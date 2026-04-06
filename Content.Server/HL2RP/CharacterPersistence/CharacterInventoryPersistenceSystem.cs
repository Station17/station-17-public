using System.Linq;
using Content.Server.GameTicking;
using Content.Server.Mind;
using Content.Server.Preferences.Managers;
using Content.Shared.GameTicking;
using Content.Shared.HL2RP.CharacterPersistence;
using Content.Shared.Inventory;
using Content.Shared.Stacks;
using Content.Shared.Storage;
using Content.Shared.Storage.EntitySystems;
using Robust.Server.Player;
using Robust.Shared.GameObjects;
using Robust.Shared.Player;

namespace Content.Server.HL2RP.CharacterPersistence;

// HL2RP CHANGE START: persistent character inventory behavior.
public sealed class CharacterInventoryPersistenceSystem : EntitySystem
{
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly SharedStorageSystem _storage = default!;
    [Dependency] private readonly SharedStackSystem _stack = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly IPlayerManager _player = default!;
    [Dependency] private readonly IServerPreferencesManager _prefs = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
        SubscribeLocalEvent<RoundRestartCleanupEvent>(OnRoundRestartCleanup);
        SubscribeLocalEvent<PlayerSpawnCompleteEvent>(OnPlayerSpawned);
    }

    private void OnPlayerDetached(PlayerDetachedEvent ev)
    {
        SaveCharacterInventory(ev.Player);
    }

    private void OnRoundRestartCleanup(RoundRestartCleanupEvent ev)
    {
        foreach (var session in _player.Sessions)
        {
            SaveCharacterInventory(session);
        }
    }

    private void OnPlayerSpawned(PlayerSpawnCompleteEvent ev)
    {
        if (!_prefs.TryGetCachedPreferences(ev.Player.UserId, out var prefs))
            return;

        if (!prefs.Characters.TryGetValue(prefs.SelectedCharacterIndex, out var profile))
            return;

        if (profile.SavedInventory.EquippedSlots.Count == 0)
            return;

        foreach (var (slotName, saved) in profile.SavedInventory.EquippedSlots)
        {
            // HL2RP CHANGE START: overwrite spawn-loadout slot contents with persisted item.
            if (_inventory.TryGetSlotEntity(ev.Mob, slotName, out var existing))
                Del(existing.Value);
            // HL2RP CHANGE END: overwrite spawn-loadout slot contents with persisted item.

            var item = SpawnSavedEntry(saved);
            if (item == null)
                continue;

            if (!_inventory.TryEquip(ev.Mob, item.Value, slotName, silent: true, force: true))
            {
                Del(item.Value);
            }
        }
    }

    private EntityUid? SpawnSavedEntry(SavedInventoryEntry entry)
    {
        var spawned = Spawn(entry.Prototype);
        if (MetaData(spawned).EntityPrototype == null)
        {
            Del(spawned);
            return null;
        }

        if (TryComp<StackComponent>(spawned, out var stack) && entry.StackCount > 0)
        {
            _stack.SetCount(spawned, entry.StackCount, stack);
        }

        if (entry.StorageContents.Count == 0 || !TryComp<StorageComponent>(spawned, out var storageComp))
            return spawned;

        foreach (var childEntry in entry.StorageContents)
        {
            var child = SpawnSavedEntry(childEntry);
            if (child == null)
                continue;

            if (!_storage.Insert(spawned, child.Value, out _, storageComp: storageComp, playSound: false))
                Del(child.Value);
        }

        return spawned;
    }

    private async void SaveCharacterInventory(ICommonSession session)
    {
        if (!_prefs.TryGetCachedPreferences(session.UserId, out var prefs))
            return;

        if (!prefs.Characters.TryGetValue(prefs.SelectedCharacterIndex, out var profile))
            return;

        EntityUid? character = session.AttachedEntity;
        if (character == null && _mind.TryGetMind(session, out _, out var mindComp))
            character = mindComp.OwnedEntity;

        if (character == null || !HasComp<InventoryComponent>(character.Value))
            return;

        var snapshot = new CharacterInventorySnapshot();

        var slots = _inventory.GetSlotEnumerator(character.Value);
        while (slots.NextItem(out var item, out var slot))
        {
            var entry = CaptureItem(item);
            if (entry == null)
                continue;

            snapshot.EquippedSlots[slot.Name] = entry;
        }

        var updated = profile.WithSavedInventory(snapshot);
        await _prefs.SetProfile(session.UserId, prefs.SelectedCharacterIndex, updated, bypassCharacterLock: true);
    }

    private SavedInventoryEntry? CaptureItem(EntityUid item)
    {
        if (HasComp<UnSaveableComponent>(item))
            return null;

        var meta = MetaData(item);
        if (meta.EntityPrototype == null)
            return null;

        var entry = new SavedInventoryEntry
        {
            Prototype = meta.EntityPrototype.ID,
            StackCount = TryComp<StackComponent>(item, out var stack) ? stack.Count : 1,
        };

        if (!TryComp<StorageComponent>(item, out var storage))
            return entry;

        foreach (var child in storage.Container.ContainedEntities.ToArray())
        {
            var childEntry = CaptureItem(child);
            if (childEntry != null)
                entry.StorageContents.Add(childEntry);
        }

        return entry;
    }
}
// HL2RP CHANGE END
