using System;
using Robust.Shared.Serialization;

namespace Content.Shared.HL2RP.CID;

/// <summary>
/// Permissions granted by a CID card when used as the main card in a CID tablet.
/// </summary>
[Serializable, NetSerializable]
[Flags]
public enum CidTabletPermissions : uint
{
    None = 0,
    IssueCards = 1 << 0,
    ViewExtendedCitizenInfo = 1 << 1,
    EditLoyaltyPoints = 1 << 2,
    Denunciations = 1 << 3,
    /// <summary>Change target citizen job to any role in the same department (intersection of department prototypes).</summary>
    ChangeJobDepartment = 1 << 4,
    /// <summary>Change target citizen job to any selectable job.</summary>
    ChangeJob = 1 << 5,
}

public static class CidTabletPermissionsExtensions
{
    /// <summary>
    /// Maps the legacy scalar access level to granular tablet permissions.
    /// </summary>
    public static CidTabletPermissions FromLegacyAccess(int access)
    {
        return access switch
        {
            <= 1 => CidTabletPermissions.None,
            2 => CidTabletPermissions.IssueCards,
            _ => CidTabletPermissions.IssueCards
                 | CidTabletPermissions.ViewExtendedCitizenInfo
                 | CidTabletPermissions.EditLoyaltyPoints
                 | CidTabletPermissions.Denunciations,
        };
    }
}
