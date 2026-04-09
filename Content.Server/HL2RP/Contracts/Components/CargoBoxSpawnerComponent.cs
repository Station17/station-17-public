using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Server.HL2RP.Contracts.Components;

[RegisterComponent]
public sealed partial class CargoBoxSpawnerComponent : Component
{
    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>), required: true)]
    public string BoxPrototype = "HL2RPContractCargoBox";

    [DataField]
    public float SpawnIntervalSeconds = 60f;

    [DataField]
    public TimeSpan NextSpawnAt;
}
