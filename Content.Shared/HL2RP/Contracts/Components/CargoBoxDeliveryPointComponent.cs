namespace Content.Shared.HL2RP.Contracts.Components;

[RegisterComponent]
public sealed partial class CargoBoxDeliveryPointComponent : Component
{
    [DataField]
    public TimeSpan DeliverDoAfter = TimeSpan.FromSeconds(2);
}
