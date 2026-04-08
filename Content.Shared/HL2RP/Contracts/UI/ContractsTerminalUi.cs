using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared.HL2RP.Contracts.UI;

[Serializable, NetSerializable]
public enum ContractsTerminalUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed record ContractListEntry(
    string Id,
    string Title,
    int RequiredCount,
    int RewardLp,
    int RewardTokens);

[Serializable, NetSerializable]
public sealed class ContractsTerminalBoundUiState : BoundUserInterfaceState
{
    public bool HasCard { get; }
    public int CardLp { get; }
    public int CardTokens { get; }

    public string? ActiveContractId { get; }
    public string? ActiveContractTitle { get; }
    public int ActiveProgress { get; }
    public int ActiveRequired { get; }

    public int CancelPenaltyLp { get; }
    public int CancelPenaltyTokens { get; }

    public List<ContractListEntry> Contracts { get; }

    public ContractsTerminalBoundUiState(
        bool hasCard,
        int cardLp,
        int cardTokens,
        string? activeContractId,
        string? activeContractTitle,
        int activeProgress,
        int activeRequired,
        int cancelPenaltyLp,
        int cancelPenaltyTokens,
        List<ContractListEntry> contracts)
    {
        HasCard = hasCard;
        CardLp = cardLp;
        CardTokens = cardTokens;
        ActiveContractId = activeContractId;
        ActiveContractTitle = activeContractTitle;
        ActiveProgress = activeProgress;
        ActiveRequired = activeRequired;
        CancelPenaltyLp = cancelPenaltyLp;
        CancelPenaltyTokens = cancelPenaltyTokens;
        Contracts = contracts;
    }
}

[Serializable, NetSerializable]
public sealed class ContractsAcceptMessage : BoundUserInterfaceMessage
{
    public string ContractId { get; }

    public ContractsAcceptMessage(string contractId)
    {
        ContractId = contractId;
    }
}

[Serializable, NetSerializable]
public sealed class ContractsCancelMessage : BoundUserInterfaceMessage
{
}
