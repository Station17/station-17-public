using Robust.Shared.GameStates;

namespace Content.Shared.HL2RP.CharacterPersistence;

// HL2RP CHANGE START: marker for items excluded from persistence.
/// <summary>
/// Items with this marker are ignored by character inventory persistence.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class UnSaveableComponent : Component
{
}
// HL2RP CHANGE END
