using System;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Hands.Systems;
using Content.Server.Inventory;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Server.Roles.Jobs;
using Content.Server.Stack;
using Content.Server.HL2RP.CID.Services;
using Content.Shared.Access.Components;
using Content.Shared.CCVar;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.HL2RP.CharacterPersistence.Components;
using Content.Shared.HL2RP.CID.Components;
using Content.Shared.Inventory;
using Content.Shared.Mobs.Systems;
using Content.Shared.Preferences;
using Content.Shared.Roles;
using Robust.Server.Player;
using Robust.Shared.Configuration;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;
using Robust.Shared.Reflection;

namespace Content.Server.HL2RP.CharacterPersistence;

public sealed class CharacterInventoryPersistenceSystem : EntitySystem
{
    [Dependency] private readonly IServerDbManager _db = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IServerPreferencesManager _preferences = default!;
    [Dependency] private readonly ServerInventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly SharedContainerSystem _containers = default!;
    [Dependency] private readonly StackSystem _stack = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly IComponentFactory _componentFactory = default!;
    [Dependency] private readonly MobStateSystem _mobState = default!;
    [Dependency] private readonly IConfigurationManager _cfg = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly JobSystem _jobs = default!;

    private readonly Dictionary<NetUserId, EntityUid> _spawnedMobs = new();
    private CIDNumberGenerator _cidNumbers = default!;

    private bool MetaProgressionEnabled => _cfg.GetCVar(CCVars.GameMetaProgressionEnabled);

    public override void Initialize()
    {
        _cidNumbers = new CIDNumberGenerator();
        _cidNumbers.Initialize();

        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
        SubscribeLocalEvent<RoundEndedEvent>(OnRoundEnded);
        _players.PlayerStatusChanged += OnPlayerStatusChanged;
    }

    public override void Shutdown()
    {
        _players.PlayerStatusChanged -= OnPlayerStatusChanged;
        base.Shutdown();
    }

    private async void OnPlayerStatusChanged(object? sender, SessionStatusEventArgs args)
    {
        if (!MetaProgressionEnabled)
            return;

        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        if (args.Session.AttachedEntity is not { Valid: true } attached)
            return;

        await SaveSnapshot(args.Session.UserId, attached);
    }

    private async void OnRoundEnded(RoundEndedEvent ev)
    {
        if (!MetaProgressionEnabled)
            return;

        // Save is intentionally spread over multiple ticks to avoid hard frame stalls at round end.
        var roundEndedAt = DateTime.UtcNow;
        var roundEndMobs = new Dictionary<NetUserId, EntityUid>(_spawnedMobs);

        foreach (var session in _players.Sessions)
        {
            if (session.AttachedEntity is { Valid: true } attached)
                roundEndMobs.TryAdd(session.UserId, attached);
        }

        foreach (var (userId, mob) in roundEndMobs)
        {
            if (!Exists(mob))
                continue;

            await Task.Yield();
            await SaveSnapshot(userId, mob);
            await SaveHistoryAndPermadeath(userId, mob, ev.RoundId, roundEndedAt);
            await PersistMetaJobHighPriorityAsync(userId, mob);
        }
    }

    private async Task PersistMetaJobHighPriorityAsync(NetUserId userId, EntityUid mob)
    {
        if (!_mind.TryGetMind(mob, out var mindId, out var mind))
            return;

        if (mind.UserId is not { } uid || uid != userId)
            return;

        if (!_jobs.MindTryGetJobId(mindId, out var jobId))
            return;

        var prefs = _preferences.GetPreferencesOrNull(userId);
        if (prefs == null)
            return;

        await _preferences.PersistMetaJobHighPriorityAsync(userId, prefs.SelectedCharacterIndex, jobId.Value);
    }

    private async void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        _spawnedMobs[ev.Player.UserId] = ev.Mob;
        if (!MetaProgressionEnabled)
            return;

        var hadSnapshot = await TryRestoreSnapshot(ev.Player.UserId, ev.Mob);
        if (!hadSnapshot)
            TryGrantInitialCidFromJob(ev.Mob, ev.JobId, ev.Profile);
    }

    private async Task SaveSnapshot(NetUserId userId, EntityUid mob)
    {
        if (!MetaProgressionEnabled)
            return;

        var prefs = _preferences.GetPreferencesOrNull(userId);
        if (prefs == null)
            return;

        var data = BuildSnapshot(mob);
        var json = JsonSerializer.SerializeToDocument(data);
        await _db.SaveCharacterInventorySnapshotAsync(userId, prefs.SelectedCharacterIndex, json);
    }

    private async Task SaveHistoryAndPermadeath(NetUserId userId, EntityUid mob, int roundId, DateTime roundEndedAt)
    {
        if (!MetaProgressionEnabled)
            return;

        var prefs = _preferences.GetPreferencesOrNull(userId);
        if (prefs == null)
            return;

        var slot = prefs.SelectedCharacterIndex;
        if (!prefs.Characters.TryGetValue(slot, out var selected))
        {
            var fallback = prefs.Characters.FirstOrDefault();
            if (fallback.Value == null)
                return;

            slot = fallback.Key;
            selected = fallback.Value;
        }

        if (selected == null)
            return;

        if (selected.Species != HumanoidCharacterProfile.DefaultSpecies)
            return;

        if (!_mobState.IsDead(mob))
            return;

        var snapshot = JsonSerializer.SerializeToDocument(BuildSnapshot(mob));
        var (name, surname) = SplitName(selected.Name);
        await _db.AppendCharacterHistorySnapshotAsync(userId, slot, roundId, roundEndedAt, name, surname, snapshot);

        if (selected.IsPermanentlyDead)
        {
            await _preferences.RefreshPreferences(userId);
            return;
        }

        await _preferences.SetProfile(userId, slot, selected.WithPermanentDeath(true), bypassLock: true);
        await _preferences.RefreshPreferences(userId);
    }

    /// <returns>True if a saved inventory snapshot existed and was applied.</returns>
    private async Task<bool> TryRestoreSnapshot(NetUserId userId, EntityUid mob)
    {
        if (!MetaProgressionEnabled)
            return false;

        var prefs = _preferences.GetPreferencesOrNull(userId);
        if (prefs == null)
            return false;

        var snapshot = await _db.GetCharacterInventorySnapshotAsync(userId, prefs.SelectedCharacterIndex);
        if (snapshot == null)
            return false;

        CharacterInventorySnapshotData? data;
        try
        {
            data = snapshot.Deserialize<CharacterInventorySnapshotData>();
        }
        catch
        {
            return false;
        }

        if (data == null)
            return false;

        ClearInventoryAndHands(mob);
        RestoreInventory(mob, data);
        return true;
    }

    private void TryGrantInitialCidFromJob(EntityUid mob, string? jobId, HumanoidCharacterProfile profile)
    {
        if (string.IsNullOrEmpty(jobId)
            || !_prototype.TryIndex<JobPrototype>(jobId, out var job)
            || job.CidCardSpawn is not { } spawn)
        {
            return;
        }

        var card = Spawn("CIDCard", Transform(mob).Coordinates);
        if (!TryComp<CIDCardComponent>(card, out var cid) || !TryComp<IdCardComponent>(card, out var idCard))
        {
            Del(card);
            return;
        }

        var cNumber = _cidNumbers.GenerateUniqueNumber();
        cid.CNumber = cNumber;
        cid.LPCount = spawn.LPCount;
        cid.LPLevel = 0;
        cid.TokensCount = 0;
        cid.TabletPermissions = spawn.TabletPermissions;
        cid.Job = spawn.Job;
        cid.IsBlank = false;
        Dirty(card, cid);

        var (first, last) = SplitName(profile.Name);
        idCard.FullName = string.IsNullOrEmpty(last) ? first : $"{first} {last}";
        idCard.LocalizedJobTitle = spawn.Job;
        Dirty(card, idCard);

        if (_inventory.TryEquip(mob, card, "id", silent: true, force: true))
            return;

        if (TryComp<HandsComponent>(mob, out var hands))
        {
            foreach (var handId in hands.Hands.Keys)
            {
                if (_hands.TryPickup(mob, card, handId, checkActionBlocker: false, animate: false))
                    return;
            }
        }
    }

    private CharacterInventorySnapshotData BuildSnapshot(EntityUid mob)
    {
        var data = new CharacterInventorySnapshotData();

        if (TryComp<InventoryComponent>(mob, out var inventory))
        {
            var slots = _inventory.GetSlotEnumerator((mob, inventory));
            while (slots.NextItem(out var item, out var slot))
            {
                var itemData = SerializeItem(item);
                if (itemData == null)
                    continue;

                data.InventorySlots.Add(new SlotItemData
                {
                    SlotId = slot.Name,
                    Item = itemData
                });
            }
        }

        if (TryComp<HandsComponent>(mob, out var hands))
        {
            foreach (var hand in hands.Hands.Keys)
            {
                if (!_hands.TryGetHeldItem((mob, hands), hand, out var held) || held == null)
                    continue;

                var itemData = SerializeItem(held.Value);
                if (itemData == null)
                    continue;

                data.Hands.Add(new HandItemData
                {
                    HandId = hand,
                    Item = itemData
                });
            }
        }

        return data;
    }

    private static (string Name, string Surname) SplitName(string fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return (string.Empty, string.Empty);

        var trimmed = fullName.Trim();
        var split = trimmed.LastIndexOf(' ');
        if (split <= 0 || split >= trimmed.Length - 1)
            return (trimmed, string.Empty);

        return (trimmed[..split], trimmed[(split + 1)..]);
    }

    private SnapshotItemData? SerializeItem(EntityUid item)
    {
        if (HasComp<UnSaveableComponent>(item))
            return null;

        var prototypeId = MetaData(item).EntityPrototype?.ID;
        if (prototypeId == null)
            return null;

        var data = new SnapshotItemData
        {
            Prototype = prototypeId,
        };

        if (TryComp<Content.Shared.Stacks.StackComponent>(item, out var stack))
            data.StackCount = stack.Count;

        if (HasComp<SaveCompsComponent>(item))
        {
            foreach (var component in AllComps(item))
            {
                if (!ShouldPersistComponent(component.GetType()))
                    continue;

                var componentName = _componentFactory.GetComponentName(component.GetType());

                data.ComponentStates.Add(new SnapshotComponentData
                {
                    Name = componentName,
                    Fields = SerializeComponentFields(component)
                });
            }
        }

        if (TryComp<ContainerManagerComponent>(item, out var manager))
        {
            foreach (var (containerId, container) in manager.Containers)
            {
                foreach (var child in container.ContainedEntities)
                {
                    var childData = SerializeItem(child);
                    if (childData == null)
                        continue;

                    if (!data.Containers.TryGetValue(containerId, out var list))
                    {
                        list = new List<SnapshotItemData>();
                        data.Containers[containerId] = list;
                    }

                    list.Add(childData);
                }
            }
        }

        return data;
    }

    private void ClearInventoryAndHands(EntityUid mob)
    {
        if (TryComp<InventoryComponent>(mob, out var inventory))
        {
            var slots = _inventory.GetSlotEnumerator((mob, inventory));
            while (slots.NextItem(out _, out var slot))
            {
                if (_inventory.TryUnequip(mob, slot.Name, out var removed, silent: true, force: true) && removed != null)
                    Del(removed.Value);
            }
        }

        if (TryComp<HandsComponent>(mob, out var hands))
        {
            foreach (var hand in hands.Hands.Keys)
            {
                if (_hands.TryGetHeldItem((mob, hands), hand, out var held) && held != null)
                    Del(held.Value);
            }
        }
    }

    private void RestoreInventory(EntityUid mob, CharacterInventorySnapshotData data)
    {
        foreach (var slot in data.InventorySlots)
        {
            var item = SpawnFromSnapshot(mob, slot.Item);
            if (item == null)
                continue;

            if (!_inventory.TryEquip(mob, item.Value, slot.SlotId, silent: true, force: true))
                Del(item.Value);
        }

        foreach (var hand in data.Hands)
        {
            var item = SpawnFromSnapshot(mob, hand.Item);
            if (item == null)
                continue;

            if (!_hands.TryPickup(mob, item.Value, hand.HandId, checkActionBlocker: false, animate: false))
                Del(item.Value);
        }
    }

    private EntityUid? SpawnFromSnapshot(EntityUid mob, SnapshotItemData data)
    {
        if (!_prototype.HasIndex<EntityPrototype>(data.Prototype))
            return null;

        var item = Spawn(data.Prototype, Transform(mob).Coordinates);

        if (data.StackCount is { } count && TryComp<Content.Shared.Stacks.StackComponent>(item, out _))
            _stack.SetCount((item, null), count);

        foreach (var (containerId, children) in data.Containers)
        {
            if (!_containers.TryGetContainer(item, containerId, out var container))
                continue;

            foreach (var child in children)
            {
                var childEntity = SpawnFromSnapshot(mob, child);
                if (childEntity == null)
                    continue;

                if (!_containers.Insert(childEntity.Value, container))
                    Del(childEntity.Value);
            }
        }

        RestoreComponentStates(item, data);

        return item;
    }

    private void RestoreComponentStates(EntityUid item, SnapshotItemData data)
    {
        // The marker component is used to decide whether we *save* component state.
        // When restoring, the spawned prototype may not include that marker, so we must not gate on it.
        if (data.ComponentStates.Count == 0)
            return;

        foreach (var componentData in data.ComponentStates)
        {
            if (!_componentFactory.TryGetRegistration(componentData.Name, out var registration))
                continue;

            if (!ShouldPersistComponent(registration.Type))
                continue;

            if (!EntityManager.TryGetComponent(item, registration.Type, out var component))
            {
                // Component existed on the saved entity instance but isn't present on the prototype.
                // Re-add it so we can restore its state.
                if (_componentFactory.GetComponent(registration.Type) is not Component created)
                    continue;

                EntityManager.AddComponent(item, created);

                if (!EntityManager.TryGetComponent(item, registration.Type, out component))
                    continue;
            }

            try
            {
                RestoreComponentFields(component, componentData.Fields);
            }
            catch
            {
                // Never let one broken component state crash player spawn.
            }
        }
    }

    private static bool ShouldPersistComponent(Type componentType)
    {
        // HL2RP safety mode: only persist explicitly HL2RP components.
        // This prevents restoring arbitrary game/runtime components with invariants
        // (e.g. GravityAffected/Bloodstream-related states) from stale snapshots.
        var ns = componentType.Namespace ?? string.Empty;
        if (!ns.Contains(".HL2RP.", StringComparison.Ordinal))
            return false;

        // Skip persistence marker itself.
        if (componentType == typeof(SaveCompsComponent))
            return false;

        return true;
    }

    private static Dictionary<string, string> SerializeComponentFields(IComponent component)
    {
        var data = new Dictionary<string, string>();
        var componentType = component.GetType();

        foreach (var prop in componentType.GetProperties(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public))
        {
            if (!prop.CanRead || !prop.CanWrite || prop.GetIndexParameters().Length != 0)
                continue;

            if (prop.GetCustomAttributes(typeof(DataFieldAttribute), true).Length == 0)
                continue;

            if (!TrySerializeMemberValue(prop.PropertyType, prop.GetValue(component), out var serialized))
                continue;

            data[prop.Name] = serialized;
        }

        foreach (var field in componentType.GetFields(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic))
        {
            if (field.IsStatic || field.IsInitOnly || field.IsLiteral)
                continue;

            if (!field.IsPublic && field.GetCustomAttributes(typeof(DataFieldAttribute), true).Length == 0)
                continue;

            if (!TrySerializeMemberValue(field.FieldType, field.GetValue(component), out var serialized))
                continue;

            data[field.Name] = serialized;
        }

        return data;
    }

    private static void RestoreComponentFields(IComponent component, Dictionary<string, string> fields)
    {
        var componentType = component.GetType();

        foreach (var (name, value) in fields)
        {
            var prop = componentType.GetProperty(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public);
            if (prop != null && prop.CanWrite && prop.GetIndexParameters().Length == 0)
            {
                if (TryDeserializeMemberValue(prop.PropertyType, value, out var parsed))
                    prop.SetValue(component, parsed);

                continue;
            }

            var field = componentType.GetField(name, System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
            if (field == null || field.IsStatic || field.IsInitOnly || field.IsLiteral)
                continue;

            if (TryDeserializeMemberValue(field.FieldType, value, out var parsedField))
                field.SetValue(component, parsedField);
        }
    }

    private static bool TrySerializeMemberValue(Type memberType, object? value, out string serialized)
    {
        if (!IsSafePersistedMemberType(memberType))
        {
            serialized = string.Empty;
            return false;
        }

        try
        {
            serialized = JsonSerializer.Serialize(value, memberType);
            return true;
        }
        catch
        {
            serialized = string.Empty;
            return false;
        }
    }

    private static bool TryDeserializeMemberValue(Type memberType, string serialized, out object? value)
    {
        if (!IsSafePersistedMemberType(memberType))
        {
            value = null;
            return false;
        }

        try
        {
            value = JsonSerializer.Deserialize(serialized, memberType);
            return true;
        }
        catch
        {
            value = null;
            return false;
        }
    }

    private static bool IsSafePersistedMemberType(Type memberType)
    {
        // Never serialize/restore component references or runtime entity/system objects.
        if (typeof(IComponent).IsAssignableFrom(memberType))
            return false;

        if (typeof(EntityUid).IsAssignableFrom(memberType) ||
            typeof(NetEntity).IsAssignableFrom(memberType) ||
            typeof(IEntitySystem).IsAssignableFrom(memberType))
            return false;

        var ns = memberType.Namespace ?? string.Empty;
        if (ns.StartsWith("Robust.", StringComparison.Ordinal) &&
            !ns.StartsWith("Robust.Shared.Maths", StringComparison.Ordinal))
            return false;

        return true;
    }

    private sealed class CharacterInventorySnapshotData
    {
        public List<SlotItemData> InventorySlots { get; set; } = new();
        public List<HandItemData> Hands { get; set; } = new();
    }

    private sealed class SlotItemData
    {
        public string SlotId { get; set; } = string.Empty;
        public SnapshotItemData Item { get; set; } = new();
    }

    private sealed class HandItemData
    {
        public string HandId { get; set; } = string.Empty;
        public SnapshotItemData Item { get; set; } = new();
    }

    private sealed class SnapshotItemData
    {
        public string Prototype { get; set; } = string.Empty;
        public int? StackCount { get; set; }
        public Dictionary<string, List<SnapshotItemData>> Containers { get; set; } = new();
        public List<SnapshotComponentData> ComponentStates { get; set; } = new();
    }

    private sealed class SnapshotComponentData
    {
        public string Name { get; set; } = string.Empty;
        public Dictionary<string, string> Fields { get; set; } = new();
    }
}
