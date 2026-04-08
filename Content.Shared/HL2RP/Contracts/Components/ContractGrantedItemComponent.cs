using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.HL2RP.Contracts.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ContractGrantedItemComponent : Component
{
    [DataField(required: true)]
    public string ContractId = string.Empty;

    // So cancel can clean up even if item is dropped/moved,
    // and so other players can't use your contract items.
    [DataField(required: true)]
    public NetEntity GrantedTo;
}

