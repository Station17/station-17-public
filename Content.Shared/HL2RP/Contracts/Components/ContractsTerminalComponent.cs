using Content.Shared.Containers.ItemSlots;
using Robust.Shared.GameStates;

namespace Content.Shared.HL2RP.Contracts.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ContractsTerminalComponent : Component
{
    public const string CardSlotId = "contracts-main-card";

    [DataField("cardSlot")]
    public ItemSlot CardSlot = new();

    [ViewVariables]
    public EntityUid? InsertedCard;
}
