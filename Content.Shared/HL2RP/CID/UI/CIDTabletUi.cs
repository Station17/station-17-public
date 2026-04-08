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
    Issue
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
public sealed class CIDTabletBoundUiState : BoundUserInterfaceState
{
    public string Name { get; }
    public string Surname { get; }
    public string CNumber { get; }
    public int LPCount { get; }
    public int LPLevel { get; }
    public string Job { get; }
    public bool CanIssue { get; }
    public bool CanViewDetails { get; }
    public bool HasIssueCard { get; }
    public string? GeneratedNumber { get; }
    public List<CIDDatabaseRecord> Records { get; }
    public CIDRecordDetails? SelectedRecord { get; }

    public CIDTabletBoundUiState(
        string name,
        string surname,
        string cNumber,
        int lpCount,
        int lpLevel,
        string job,
        bool canIssue,
        bool canViewDetails,
        bool hasIssueCard,
        string? generatedNumber,
        List<CIDDatabaseRecord> records,
        CIDRecordDetails? selectedRecord)
    {
        Name = name;
        Surname = surname;
        CNumber = cNumber;
        LPCount = lpCount;
        LPLevel = lpLevel;
        Job = job;
        CanIssue = canIssue;
        CanViewDetails = canViewDetails;
        HasIssueCard = hasIssueCard;
        GeneratedNumber = generatedNumber;
        Records = records;
        SelectedRecord = selectedRecord;
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
