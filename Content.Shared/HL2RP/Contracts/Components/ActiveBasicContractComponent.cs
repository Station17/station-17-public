using Robust.Shared.GameStates;

namespace Content.Shared.HL2RP.Contracts.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class ActiveBasicContractComponent : Component
{
    [DataField(required: true), AutoNetworkedField]
    public string ContractId = string.Empty;

    [DataField, AutoNetworkedField]
    public int Progress;

    [DataField, AutoNetworkedField]
    public int RequiredCount = 10;

    [DataField, AutoNetworkedField]
    public TimeSpan AcceptedAt;
}
