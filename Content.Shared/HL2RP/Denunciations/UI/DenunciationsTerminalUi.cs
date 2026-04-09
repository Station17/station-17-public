using Robust.Shared.Serialization;

namespace Content.Shared.HL2RP.Denunciations.UI;

[Serializable, NetSerializable]
public enum DenunciationsTerminalUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed record DenunciationCitizenEntry(
    NetEntity CardUid,
    string Name,
    string Surname,
    string CNumber);

[Serializable, NetSerializable]
public sealed class DenunciationsTerminalBoundUiState : BoundUserInterfaceState
{
    public bool HasReporterCid { get; }
    public string ReporterCNumber { get; }
    public List<DenunciationCitizenEntry> Citizens { get; }
    public DenunciationCitizenEntry? SelectedCitizen { get; }

    public DenunciationsTerminalBoundUiState(
        bool hasReporterCid,
        string reporterCNumber,
        List<DenunciationCitizenEntry> citizens,
        DenunciationCitizenEntry? selectedCitizen)
    {
        HasReporterCid = hasReporterCid;
        ReporterCNumber = reporterCNumber;
        Citizens = citizens;
        SelectedCitizen = selectedCitizen;
    }
}

[Serializable, NetSerializable]
public sealed class DenunciationsSelectCitizenMessage : BoundUserInterfaceMessage
{
    public NetEntity CardUid { get; }

    public DenunciationsSelectCitizenMessage(NetEntity cardUid)
    {
        CardUid = cardUid;
    }
}

[Serializable, NetSerializable]
public sealed class DenunciationsSubmitMessage : BoundUserInterfaceMessage
{
    public NetEntity TargetCardUid { get; }
    public string Reason { get; }
    public int Severity { get; }

    public DenunciationsSubmitMessage(NetEntity targetCardUid, string reason, int severity)
    {
        TargetCardUid = targetCardUid;
        Reason = reason;
        Severity = severity;
    }
}
