using Robust.Shared.GameStates;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom;

namespace Content.Shared.HL2RP.CID.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CIDCardComponent : Component
{
    [DataField, AutoNetworkedField]
    public int LPCount;

    [DataField, AutoNetworkedField]
    public int LPLevel;

    [DataField, AutoNetworkedField]
    public int TokensCount;

    [DataField, AutoNetworkedField]
    public string CNumber = string.Empty;

    [DataField, AutoNetworkedField]
    public int Access;

    [DataField, AutoNetworkedField]
    public string Job = string.Empty;

    [DataField, AutoNetworkedField]
    public bool IsBlank = true;
}
