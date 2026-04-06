using Robust.Shared.Serialization;

namespace Content.Shared.HL2RP.CharacterPersistence;

// HL2RP CHANGE START: character inventory persistence snapshot models.

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class CharacterInventorySnapshot
{
    [DataField]
    public Dictionary<string, SavedInventoryEntry> EquippedSlots { get; set; } = new();

    public CharacterInventorySnapshot Clone()
    {
        var clone = new CharacterInventorySnapshot();
        foreach (var (slot, item) in EquippedSlots)
        {
            clone.EquippedSlots[slot] = item.Clone();
        }

        return clone;
    }
}

[DataDefinition]
[Serializable, NetSerializable]
public sealed partial class SavedInventoryEntry
{
    [DataField(required: true)]
    public string Prototype { get; set; } = string.Empty;

    [DataField]
    public int StackCount { get; set; } = 1;

    [DataField]
    public List<SavedInventoryEntry> StorageContents { get; set; } = new();

    public SavedInventoryEntry Clone()
    {
        var clone = new SavedInventoryEntry
        {
            Prototype = Prototype,
            StackCount = StackCount,
        };

        foreach (var item in StorageContents)
        {
            clone.StorageContents.Add(item.Clone());
        }

        return clone;
    }
}
// HL2RP CHANGE END
