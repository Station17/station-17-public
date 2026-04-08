using Robust.Shared.Prototypes;
using Robust.Shared.Serialization.TypeSerializers.Implementations.Custom.Prototype;

namespace Content.Shared.HL2RP.Contracts.Prototypes;

[Prototype("hl2rpBasicContract")]
public sealed partial class BasicContractPrototype : IPrototype
{
    [IdDataField]
    public string ID { get; private set; } = default!;

    [DataField(required: true)]
    public string Title = string.Empty;

    [DataField]
    public int RequiredCount = 10;

    [DataField]
    public int RewardLp = 1;

    [DataField]
    public int RewardTokens = 150;

    [DataField]
    public int CancelPenaltyLp = 1;

    [DataField]
    public int CancelPenaltyTokens = 50;

    [DataField(customTypeSerializer: typeof(PrototypeIdSerializer<EntityPrototype>))]
    public string ItemToGive = string.Empty;

    [DataField]
    public int ItemCount = 10;

    [DataField]
    public string ObjectiveType = "PosterPaste";
}
