using Robust.Shared.GameStates;
using Robust.Shared.Serialization;

namespace Content.Shared.HL2RP.Contracts.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class CargoBoxContractItemComponent : Component
{
    [DataField, AutoNetworkedField]
    public NetEntity Spawner;

    [DataField, AutoNetworkedField]
    public bool HighlightEnabled;
}
