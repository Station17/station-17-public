using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;

namespace Content.Shared.HL2RP.CID.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class CIDTabletComponent : Component
{
    public const string MainCardSlotId = "cid-main-card";
    public const string IssueCardSlotId = "cid-issue-card";

    [DataField("mainCardSlot")]
    public ItemSlot MainCardSlot = new();

    [DataField("issueCardSlot")]
    public ItemSlot IssueCardSlot = new();

    [ViewVariables]
    public EntityUid? MainCard;

    [ViewVariables]
    public EntityUid? IssueCard;
}
