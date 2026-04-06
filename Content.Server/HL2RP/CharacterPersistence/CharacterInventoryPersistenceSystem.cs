using System.Text.Json;
using System.Threading.Tasks;
using Content.Server.Database;
using Content.Server.Hands.Systems;
using Content.Server.Inventory;
using Content.Server.Preferences.Managers;
using Content.Server.Stack;
using Content.Shared.GameTicking;
using Content.Shared.Hands.Components;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.HL2RP.CharacterPersistence.Components;
using Content.Shared.Inventory;
using Robust.Server.Player;
using Robust.Shared.Containers;
using Robust.Shared.Enums;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

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

    private readonly Dictionary<NetUserId, EntityUid> _spawnedMobs = new();

    public override void Initialize()
    {
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
        if (args.NewStatus != SessionStatus.Disconnected)
            return;

        if (args.Session.AttachedEntity is not { Valid: true } attached)
            return;

        await SaveSnapshot(args.Session.UserId, attached);
    }

    private async void OnRoundEnded(RoundEndedEvent ev)
    {
        foreach (var (userId, mob) in _spawnedMobs)
        {
            if (!Exists(mob))
                continue;

            await SaveSnapshot(userId, mob);
        }
    }

    private async void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        _spawnedMobs[ev.Player.UserId] = ev.Mob;
        await RestoreSnapshot(ev.Player.UserId, ev.Mob);
    }

    private async Task SaveSnapshot(NetUserId userId, EntityUid mob)
    {
        var prefs = _preferences.GetPreferencesOrNull(userId);
        if (prefs == null)
            return;

        var data = BuildSnapshot(mob);
        var json = JsonSerializer.SerializeToDocument(data);
        await _db.SaveCharacterInventorySnapshotAsync(userId, prefs.SelectedCharacterIndex, json);
    }

    private async Task RestoreSnapshot(NetUserId userId, EntityUid mob)
    {
        var prefs = _preferences.GetPreferencesOrNull(userId);
        if (prefs == null)
            return;

        var snapshot = await _db.GetCharacterInventorySnapshotAsync(userId, prefs.SelectedCharacterIndex);
        if (snapshot == null)
            return;

        CharacterInventorySnapshotData? data;
        try
        {
            data = snapshot.Deserialize<CharacterInventorySnapshotData>();
        }
        catch
        {
            return;
        }

        if (data == null)
            return;

        ClearInventoryAndHands(mob);
        RestoreInventory(mob, data);
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

        return item;
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
    }
}
