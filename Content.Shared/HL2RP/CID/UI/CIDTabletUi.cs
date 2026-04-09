using Robust.Shared.Serialization;

namespace Content.Shared.HL2RP.CID.UI;

[Serializable, NetSerializable]
public enum CIDTabletUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum CIDTabletTab : byte
{
    Info,
    Database,
    Issue,
    Reports
}

[Serializable, NetSerializable]
public sealed record CIDDatabaseRecord(
    NetEntity CardUid,
    string Name,
    string Surname,
    string CNumber);

[Serializable, NetSerializable]
public sealed record CIDRecordDetails(
    NetEntity CardUid,
    string Name,
    string Surname,
    string CNumber,
    int LPCount,
    int LPLevel,
    int TokensCount,
    int Access,
    string Job);

[Serializable, NetSerializable]
public sealed record CIDDenunciationListEntry(
    int Id,
    string TargetName,
    string TargetSurname,
    string TargetCNumber,
    int Severity);

[Serializable, NetSerializable]
public sealed record CIDDenunciationDetails(
    int Id,
    string TargetName,
    string TargetSurname,
    string TargetCNumber,
    string ReporterName,
    string ReporterSurname,
    string ReporterCNumber,
    string Reason,
    int Severity,
    string? ResolverName,
    string? ResolverSurname,
    string? ResolverCNumber,
    bool CanTake,
    bool CanControlResolution);

[Serializable, NetSerializable]
public sealed class CIDTabletBoundUiState : BoundUserInterfaceState
{
    public string Name { get; }
    public string Surname { get; }
    public string CNumber { get; }
    public int LPCount { get; }
    public int LPLevel { get; }
    public int TokensCount { get; }
    public string Job { get; }
    public bool CanIssue { get; }
    public bool CanViewDetails { get; }
    public bool HasIssueCard { get; }
    public string? GeneratedNumber { get; }
    public List<CIDDatabaseRecord> Records { get; }
    public CIDRecordDetails? SelectedRecord { get; }
    public bool CanViewDenunciations { get; }
    public List<CIDDenunciationListEntry> Denunciations { get; }
    public CIDDenunciationDetails? SelectedDenunciation { get; }

    public CIDTabletBoundUiState(
        string name,
        string surname,
        string cNumber,
        int lpCount,
        int lpLevel,
        int tokensCount,
        string job,
        bool canIssue,
        bool canViewDetails,
        bool hasIssueCard,
        string? generatedNumber,
        List<CIDDatabaseRecord> records,
        CIDRecordDetails? selectedRecord,
        bool canViewDenunciations,
        List<CIDDenunciationListEntry> denunciations,
        CIDDenunciationDetails? selectedDenunciation)
    {
        Name = name;
        Surname = surname;
        CNumber = cNumber;
        LPCount = lpCount;
        LPLevel = lpLevel;
        TokensCount = tokensCount;
        Job = job;
        CanIssue = canIssue;
        CanViewDetails = canViewDetails;
        HasIssueCard = hasIssueCard;
        GeneratedNumber = generatedNumber;
        Records = records;
        SelectedRecord = selectedRecord;
        CanViewDenunciations = canViewDenunciations;
        Denunciations = denunciations;
        SelectedDenunciation = selectedDenunciation;
    }
}

[Serializable, NetSerializable]
public sealed class CIDSelectRecordMessage : BoundUserInterfaceMessage
{
    public NetEntity CardUid { get; }

    public CIDSelectRecordMessage(NetEntity cardUid)
    {
        CardUid = cardUid;
    }
}

[Serializable, NetSerializable]
public sealed class CIDClearSelectedRecordMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CIDUpdateSelectedLPMessage : BoundUserInterfaceMessage
{
    public NetEntity CardUid { get; }
    public int LPCount { get; }

    public CIDUpdateSelectedLPMessage(NetEntity cardUid, int lpCount)
    {
        CardUid = cardUid;
        LPCount = lpCount;
    }
}

[Serializable, NetSerializable]
public sealed class CIDGenerateNumberMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CIDWriteCardMessage : BoundUserInterfaceMessage
{
    public string Name { get; }
    public string Surname { get; }
    public string CNumber { get; }

    public CIDWriteCardMessage(string name, string surname, string cNumber)
    {
        Name = name;
        Surname = surname;
        CNumber = cNumber;
    }
}

[Serializable, NetSerializable]
public sealed class CIDSelectDenunciationMessage : BoundUserInterfaceMessage
{
    public int DenunciationId { get; }

    public CIDSelectDenunciationMessage(int denunciationId)
    {
        DenunciationId = denunciationId;
    }
}

[Serializable, NetSerializable]
public sealed class CIDClearSelectedDenunciationMessage : BoundUserInterfaceMessage
{
}

[Serializable, NetSerializable]
public sealed class CIDTakeDenunciationMessage : BoundUserInterfaceMessage
{
    public int DenunciationId { get; }

    public CIDTakeDenunciationMessage(int denunciationId)
    {
        DenunciationId = denunciationId;
    }
}

[Serializable, NetSerializable]
public sealed class CIDCancelDenunciationResolutionMessage : BoundUserInterfaceMessage
{
    public int DenunciationId { get; }

    public CIDCancelDenunciationResolutionMessage(int denunciationId)
    {
        DenunciationId = denunciationId;
    }
}

[Serializable, NetSerializable]
public sealed class CIDAcceptDenunciationMessage : BoundUserInterfaceMessage
{
    public int DenunciationId { get; }

    public CIDAcceptDenunciationMessage(int denunciationId)
    {
        DenunciationId = denunciationId;
    }
}

[Serializable, NetSerializable]
public sealed class CIDRejectDenunciationMessage : BoundUserInterfaceMessage
{
    public int DenunciationId { get; }

    public CIDRejectDenunciationMessage(int denunciationId)
    {
        DenunciationId = denunciationId;
    }
}
