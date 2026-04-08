using Robust.Shared.GameStates;

namespace Content.Shared.HL2RP.Contracts.Components;

/// <summary>
/// Marker component granted to players who accepted the poster-pasting contract.
/// Client uses this to decide if paste markers should be visible.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class PosterPasteContractWorkerComponent : Component
{
}

