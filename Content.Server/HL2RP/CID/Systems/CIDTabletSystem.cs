using System.Text.RegularExpressions;
using Content.Server.HL2RP.CID.Services;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.HL2RP.CID.Components;
using Content.Shared.HL2RP.CID.Systems;
using Content.Shared.HL2RP.CID.UI;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Utility;

namespace Content.Server.HL2RP.CID.Systems;

public sealed class CIDTabletSystem : SharedCIDTabletSystem
{
    private static readonly Regex NumberRegex = new("^[0-9]{6}$", RegexOptions.Compiled);
    private const float GlobalUiRefreshInterval = 1.0f;

    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    private CIDNumberGenerator _numberGenerator = default!;

    private readonly Dictionary<EntityUid, EntityUid?> _selectedCards = new();
    private float _globalUiRefreshAccumulator;

    public override void Initialize()
    {
        base.Initialize();
        _numberGenerator = new CIDNumberGenerator();
        _numberGenerator.Initialize();

        SubscribeLocalEvent<CIDTabletComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CIDTabletComponent, CIDGenerateNumberMessage>(OnGenerateNumber);
        SubscribeLocalEvent<CIDTabletComponent, CIDWriteCardMessage>(OnWriteCard);
        SubscribeLocalEvent<CIDTabletComponent, CIDSelectRecordMessage>(OnSelectRecord);
        SubscribeLocalEvent<CIDTabletComponent, CIDClearSelectedRecordMessage>(OnClearSelectedRecord);
        SubscribeLocalEvent<CIDTabletComponent, CIDUpdateSelectedLPMessage>(OnUpdateSelectedLp);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _globalUiRefreshAccumulator += frameTime;
        if (_globalUiRefreshAccumulator < GlobalUiRefreshInterval)
            return;

        _globalUiRefreshAccumulator = 0f;
        RefreshAllOpenTabletUis();
    }

    private void OnUiOpened(Entity<CIDTabletComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnGenerateNumber(Entity<CIDTabletComponent> ent, ref CIDGenerateNumberMessage args)
    {
        UpdateUi(ent.Owner, ent.Comp, _numberGenerator.GenerateUniqueNumber());
    }
    
    private void OnSelectRecord(Entity<CIDTabletComponent> ent, ref CIDSelectRecordMessage args)
    {
        var uid = GetEntity(args.CardUid);
        _selectedCards[ent.Owner] = uid;
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnClearSelectedRecord(Entity<CIDTabletComponent> ent, ref CIDClearSelectedRecordMessage args)
    {
        _selectedCards[ent.Owner] = null;
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnUpdateSelectedLp(Entity<CIDTabletComponent> ent, ref CIDUpdateSelectedLPMessage args)
    {
        if (!TryComp<CIDCardComponent>(ent.Comp.MainCard, out var mainCid))
            return;

        if (mainCid.Access <= 2)
            return;

        var target = GetEntity(args.CardUid);
        if (!TryComp<CIDCardComponent>(target, out var cid))
            return;

        cid.LPCount = Math.Clamp(args.LPCount, -9999, 9999);
        Dirty(target, cid);
        RefreshAllOpenTabletUis();
    }

    private void OnWriteCard(Entity<CIDTabletComponent> ent, ref CIDWriteCardMessage args)
    {
        if (!TryComp<CIDCardComponent>(ent.Comp.MainCard, out var mainCid))
            return;

        if (mainCid.Access <= 1 || ent.Comp.IssueCard is not { } issueUid)
            return;

        if (!TryComp<CIDCardComponent>(issueUid, out var issueCid) || !issueCid.IsBlank)
            return;

        if (!TryComp<IdCardComponent>(issueUid, out var issueId))
            return;

        var name = SanitizeName(args.Name);
        var surname = SanitizeName(args.Surname);
        if (string.IsNullOrWhiteSpace(name) || string.IsNullOrWhiteSpace(surname))
            return;

        var cNumber = args.CNumber.Trim();
        if (!NumberRegex.IsMatch(cNumber) || _numberGenerator.IsNumberTaken(cNumber))
            return;

        issueCid.CNumber = cNumber;
        issueCid.LPCount = 0;
        issueCid.LPLevel = 0;
        issueCid.TokensCount = 0;
        issueCid.Access = 0;
        issueCid.Job = "Без должности";
        issueCid.IsBlank = false;
        Dirty(issueUid, issueCid);

        issueId.FullName = $"{name} {surname}";
        issueId.LocalizedJobTitle = issueCid.Job;
        Dirty(issueUid, issueId);

        var ejected = ItemSlots.TryEjectToHands(ent.Owner, ent.Comp.IssueCardSlot, args.Actor);
        if (!ejected)
            ItemSlots.TryEject(ent.Owner, ent.Comp.IssueCardSlot, null, out _, true);

        ent.Comp.IssueCard = null;
        RefreshAllOpenTabletUis();
    }

    private void RefreshAllOpenTabletUis()
    {
        var query = EntityQueryEnumerator<CIDTabletComponent>();
        while (query.MoveNext(out var uid, out var comp))
        {
            if (!_ui.IsUiOpen(uid, CIDTabletUiKey.Key))
                continue;

            UpdateUi(uid, comp);
        }
    }

    private void UpdateUi(EntityUid uid, CIDTabletComponent comp, string? generatedNumber = null)
    {
        if (!_ui.HasUi(uid, CIDTabletUiKey.Key))
            return;

        var canIssue = false;
        var canViewDetails = false;
        var infoName = "-";
        var infoSurname = "-";
        var infoNumber = "-";
        var lpCount = 0;
        var lpLevel = 0;
        var job = "-";

        if (comp.MainCard is { } mainCardUid &&
            TryComp<CIDCardComponent>(mainCardUid, out var mainCid))
        {
            canIssue = mainCid.Access > 1;
            canViewDetails = mainCid.Access > 2;
            infoNumber = string.IsNullOrWhiteSpace(mainCid.CNumber) ? "-" : mainCid.CNumber;
            lpCount = mainCid.LPCount;
            lpLevel = mainCid.LPLevel;
            job = string.IsNullOrWhiteSpace(mainCid.Job) ? "-" : mainCid.Job;

            if (TryComp<IdCardComponent>(mainCardUid, out var idComp))
            {
                var (n, s) = SplitName(idComp.FullName);
                infoName = n;
                infoSurname = s;
            }
        }

        var records = new List<CIDDatabaseRecord>();
        var selectedDetails = default(CIDRecordDetails);
        var selectedUid = _selectedCards.GetValueOrDefault(uid);

        var query = EntityQueryEnumerator<CIDCardComponent>();
        while (query.MoveNext(out var cardUid, out var cid))
        {
            TryComp<IdCardComponent>(cardUid, out var id);
            var (name, surname) = SplitName(id?.FullName);
            records.Add(new CIDDatabaseRecord(GetNetEntity(cardUid), name, surname, cid.CNumber));

            if (canViewDetails && selectedUid == cardUid)
            {
                selectedDetails = new CIDRecordDetails(
                    GetNetEntity(cardUid),
                    name,
                    surname,
                    cid.CNumber,
                    cid.LPCount,
                    cid.LPLevel,
                    cid.TokensCount,
                    cid.Access,
                    cid.Job);
            }
        }

        if (generatedNumber == null &&
            comp.IssueCard is { } issueCardUid &&
            TryComp<CIDCardComponent>(issueCardUid, out var issueCid) &&
            !string.IsNullOrWhiteSpace(issueCid.CNumber))
        {
            generatedNumber = issueCid.CNumber;
        }

        var state = new CIDTabletBoundUiState(
            infoName,
            infoSurname,
            infoNumber,
            lpCount,
            lpLevel,
            job,
            canIssue,
            canViewDetails,
            comp.IssueCard != null,
            generatedNumber,
            records,
            selectedDetails);

        _ui.SetUiState(uid, CIDTabletUiKey.Key, state);
    }

    private static (string name, string surname) SplitName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
            return ("-", "-");

        var parts = fullName.Split(' ', 2, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
            return ("-", "-");
        if (parts.Length == 1)
            return (parts[0], "-");

        return (parts[0], parts[1]);
    }

    private static string SanitizeName(string input)
    {
        var sanitized = FormattedMessage.RemoveMarkupPermissive(input.Trim());
        if (sanitized.Length > 32)
            sanitized = sanitized[..32];

        return sanitized;
    }
}
