using Content.Shared.HL2RP.CID;
using Robust.Shared.GameStates;

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
    public CidTabletPermissions TabletPermissions;

    [DataField("access")]
    private int? _legacyAccess;

    [DataField, AutoNetworkedField]
    public string Job = string.Empty;

    [DataField, AutoNetworkedField]
    public bool IsBlank = true;

    /// <summary>
    /// If the entity was deserialized with the old <c>access</c> field, merge it into
    /// <see cref="TabletPermissions"/> and clear the legacy value.
    /// </summary>
    /// <returns>True if the component was modified.</returns>
    public bool ApplyLegacyAccessIfPresent()
    {
        if (_legacyAccess is not { } v)
            return false;

        TabletPermissions = CidTabletPermissionsExtensions.FromLegacyAccess(v);
        _legacyAccess = null;
        return true;
    }
}
