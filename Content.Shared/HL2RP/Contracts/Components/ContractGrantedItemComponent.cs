using Robust.Shared.GameStates;

namespace Content.Shared.HL2RP.Contracts.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ContractGrantedItemComponent : Component
{
    [DataField(required: true)]
    public string ContractId = string.Empty;
}

