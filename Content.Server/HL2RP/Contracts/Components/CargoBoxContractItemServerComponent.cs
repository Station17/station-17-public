namespace Content.Server.HL2RP.Contracts.Components;

[RegisterComponent]
public sealed partial class CargoBoxContractItemServerComponent : Component
{
    [DataField]
    public TimeSpan? DeleteAt;
}
