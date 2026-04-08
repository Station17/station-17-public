using Robust.Shared.GameStates;

namespace Content.Shared.HL2RP.Contracts.Components;

/// <summary>
/// Marker component for players with the active cargo-box contract.
/// Used client-side to show highlighted cargo boxes.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class CargoBoxContractWorkerComponent : Component
{
}
