using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.HL2RP.Contracts.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class PosterPasteMarkerComponent : Component
{
    [DataField, AutoNetworkedField]
    public bool Active = true;
}

