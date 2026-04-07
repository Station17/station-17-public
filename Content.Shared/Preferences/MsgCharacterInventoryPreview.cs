using System.IO;
using Lidgren.Network;
using Robust.Shared.Network;
using Robust.Shared.Serialization;

namespace Content.Shared.Preferences;

/// <summary>
/// Server -> client: minimal snapshot data for character preview rendering.
/// </summary>
public sealed class MsgCharacterInventoryPreview : NetMessage
{
    public override MsgGroups MsgGroup => MsgGroups.Command;

    public int Slot;
    public CharacterInventoryPreviewData? Preview;

    public override void ReadFromBuffer(NetIncomingMessage buffer, IRobustSerializer serializer)
    {
        Slot = buffer.ReadVariableInt32();
        var hasPreview = buffer.ReadBoolean();
        if (!hasPreview)
        {
            Preview = null;
            return;
        }

        var length = buffer.ReadVariableInt32();
        using var stream = new MemoryStream();
        buffer.ReadAlignedMemory(stream, length);
        serializer.DeserializeDirect(stream, out CharacterInventoryPreviewData? preview);
        Preview = preview;
    }

    public override void WriteToBuffer(NetOutgoingMessage buffer, IRobustSerializer serializer)
    {
        buffer.WriteVariableInt32(Slot);
        buffer.Write(Preview != null);
        if (Preview == null)
            return;

        using var stream = new MemoryStream();
        serializer.SerializeDirect(stream, Preview);
        buffer.WriteVariableInt32((int) stream.Length);
        stream.TryGetBuffer(out var segment);
        buffer.Write(segment);
    }
}
