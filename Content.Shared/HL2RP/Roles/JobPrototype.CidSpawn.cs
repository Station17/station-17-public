using Content.Shared.HL2RP.CID;
using Robust.Shared.Serialization.Manager.Attributes;

namespace Content.Shared.Roles;

/// <summary>
/// HL2RP: default CID card data when granting a first-time metaprogression card for this job.
/// </summary>
[DataDefinition]
public sealed partial class JobPrototypeCidCardSpawnData
{
    [DataField]
    public int LPCount;

    [DataField]
    public CidTabletPermissions TabletPermissions;

    [DataField]
    public string Job = string.Empty;
}

public sealed partial class JobPrototype
{
    [DataField("cidCardSpawn")]
    public JobPrototypeCidCardSpawnData? CidCardSpawn { get; private set; }
}
