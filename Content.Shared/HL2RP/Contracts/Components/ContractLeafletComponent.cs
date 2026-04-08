using Robust.Shared.GameStates;

namespace Content.Shared.HL2RP.Contracts.Components;

[RegisterComponent, NetworkedComponent]
public sealed partial class ContractLeafletComponent : Component
{
    [DataField]
    public TimeSpan PasteDoAfter = TimeSpan.FromSeconds(5);
}

