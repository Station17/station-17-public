using Robust.Shared.Serialization;

namespace Content.Shared.Preferences;

[Serializable, NetSerializable]
public sealed class CharacterInventoryPreviewData
{
    public Dictionary<string, string> InventorySlots { get; set; } = new();
    public Dictionary<string, string> Hands { get; set; } = new();

    public bool HasAnyItems => InventorySlots.Count > 0 || Hands.Count > 0;
}
