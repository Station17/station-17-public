using System.Text.RegularExpressions;
using Content.Server.HL2RP.CID.Services;
using Content.Server.HL2RP.Denunciations.Systems;
using Content.Server.Popups;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.HL2RP.CID.Components;
using Content.Shared.HL2RP.CID.Systems;
using Content.Shared.HL2RP.CID.UI;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Utility;

namespace Content.Server.HL2RP.CID.Systems;

public sealed class CIDTabletSystem : SharedCIDTabletSystem
{
    private static readonly Regex NumberRegex = new("^[0-9]{6}$", RegexOptions.Compiled);
    private const float GlobalUiRefreshInterval = 1.0f;
    private static readonly SoundSpecifier NotifyBeep = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");

    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DenunciationsSystem _denunciations = default!;
    private CIDNumberGenerator _numberGenerator = default!;

    private readonly Dictionary<EntityUid, EntityUid?> _selectedCards = new();
    private readonly Dictionary<EntityUid, int?> _selectedDenunciations = new();
    private readonly Dictionary<EntityUid, TrackedMainCardState> _trackedMainCards = new();
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
        SubscribeLocalEvent<CIDTabletComponent, CIDSelectDenunciationMessage>(OnSelectDenunciation);
        SubscribeLocalEvent<CIDTabletComponent, CIDClearSelectedDenunciationMessage>(OnClearSelectedDenunciation);
        SubscribeLocalEvent<CIDTabletComponent, CIDTakeDenunciationMessage>(OnTakeDenunciation);
        SubscribeLocalEvent<CIDTabletComponent, CIDCancelDenunciationResolutionMessage>(OnCancelDenunciationResolution);
        SubscribeLocalEvent<CIDTabletComponent, CIDAcceptDenunciationMessage>(OnAcceptDenunciation);
        SubscribeLocalEvent<CIDTabletComponent, CIDRejectDenunciationMessage>(OnRejectDenunciation);
        _denunciations.ReportsChanged += RefreshAllOpenTabletUis;
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        _globalUiRefreshAccumulator += frameTime;
        if (_globalUiRefreshAccumulator < GlobalUiRefreshInterval)
            return;

        _globalUiRefreshAccumulator = 0f;
        ProcessInsertedMainCardChanges();
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

    private void OnSelectDenunciation(Entity<CIDTabletComponent> ent, ref CIDSelectDenunciationMessage args)
    {
        _selectedDenunciations[ent.Owner] = args.DenunciationId;
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnClearSelectedDenunciation(Entity<CIDTabletComponent> ent, ref CIDClearSelectedDenunciationMessage args)
    {
        _selectedDenunciations[ent.Owner] = null;
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnTakeDenunciation(Entity<CIDTabletComponent> ent, ref CIDTakeDenunciationMessage args)
    {
        if (!TryGetResolverCid(ent, out var resolverCid))
            return;

        _denunciations.TryTake(args.DenunciationId, resolverCid);
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnCancelDenunciationResolution(Entity<CIDTabletComponent> ent, ref CIDCancelDenunciationResolutionMessage args)
    {
        if (!TryGetResolverCid(ent, out var resolverCid))
            return;

        _denunciations.TryCancel(args.DenunciationId, resolverCid);
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnAcceptDenunciation(Entity<CIDTabletComponent> ent, ref CIDAcceptDenunciationMessage args)
    {
        if (!TryGetResolverCid(ent, out var resolverCid))
            return;

        _denunciations.TryAccept(args.DenunciationId, resolverCid);
        UpdateUi(ent.Owner, ent.Comp);
    }

    private void OnRejectDenunciation(Entity<CIDTabletComponent> ent, ref CIDRejectDenunciationMessage args)
    {
        if (!TryGetResolverCid(ent, out var resolverCid))
            return;

        _denunciations.TryReject(args.DenunciationId, resolverCid);
        UpdateUi(ent.Owner, ent.Comp);
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

    private void ProcessInsertedMainCardChanges()
    {
        var query = EntityQueryEnumerator<CIDTabletComponent>();
        while (query.MoveNext(out var tabletUid, out var tablet))
        {
            if (tablet.MainCard is not { } cardUid || !TryComp<CIDCardComponent>(cardUid, out var card))
            {
                _trackedMainCards.Remove(tabletUid);
                continue;
            }

            if (!_trackedMainCards.TryGetValue(tabletUid, out var tracked) || tracked.CardUid != cardUid)
            {
                _trackedMainCards[tabletUid] = new TrackedMainCardState(cardUid, card.LPCount, card.TokensCount);
                continue;
            }

            if (tracked.LPCount != card.LPCount)
            {
                var delta = card.LPCount - tracked.LPCount;
                _popup.PopupEntity(
                    $"Ваши очки лояльности были изменены на {card.LPCount} ({FormatDelta(delta)})",
                    tabletUid,
                    PopupType.Medium);
                _audio.PlayPvs(NotifyBeep, tabletUid);
            }

            if (tracked.TokensCount != card.TokensCount)
            {
                var delta = card.TokensCount - tracked.TokensCount;
                _popup.PopupEntity(
                    $"Ваш баланс токенов был изменен на {card.TokensCount} ({FormatDelta(delta)})",
                    tabletUid,
                    PopupType.Medium);
                _audio.PlayPvs(NotifyBeep, tabletUid);
            }

            _trackedMainCards[tabletUid] = new TrackedMainCardState(cardUid, card.LPCount, card.TokensCount);
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
        var tokensCount = 0;
        var job = "-";

        if (comp.MainCard is { } mainCardUid &&
            TryComp<CIDCardComponent>(mainCardUid, out var mainCid))
        {
            canIssue = mainCid.Access > 1;
            canViewDetails = mainCid.Access > 2;
            infoNumber = string.IsNullOrWhiteSpace(mainCid.CNumber) ? "-" : mainCid.CNumber;
            lpCount = mainCid.LPCount;
            lpLevel = mainCid.LPLevel;
            tokensCount = mainCid.TokensCount;
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
            if (cid.IsBlank)
                continue;

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

        var denunciations = new List<CIDDenunciationListEntry>();
        CIDDenunciationDetails? selectedDenunciation = null;
        var selectedDenunciationId = _selectedDenunciations.GetValueOrDefault(uid);
        if (canViewDetails)
        {
            foreach (var denunciation in _denunciations.GetEntriesSortedBySeverity())
            {
                var (targetName, targetSurname, targetCode) = GetCidIdentity(denunciation.TargetCard);
                denunciations.Add(new CIDDenunciationListEntry(
                    denunciation.Id,
                    targetName,
                    targetSurname,
                    targetCode,
                    denunciation.Severity));

                if (selectedDenunciationId != denunciation.Id)
                    continue;

                var (reporterName, reporterSurname, reporterCode) = GetCidIdentity(denunciation.ReporterCard);
                var (resolverName, resolverSurname, resolverCode) = denunciation.ResolverCard is { } resolver
                    ? GetCidIdentity(resolver)
                    : ("-", "-", "-");
                var canTake = denunciation.ResolverCard == null;
                var canControl = comp.MainCard != null && denunciation.ResolverCard == comp.MainCard;

                selectedDenunciation = new CIDDenunciationDetails(
                    denunciation.Id,
                    targetName,
                    targetSurname,
                    targetCode,
                    reporterName,
                    reporterSurname,
                    reporterCode,
                    denunciation.Reason,
                    denunciation.Severity,
                    denunciation.ResolverCard == null ? null : resolverName,
                    denunciation.ResolverCard == null ? null : resolverSurname,
                    denunciation.ResolverCard == null ? null : resolverCode,
                    canTake,
                    canControl);
            }
        }

        var state = new CIDTabletBoundUiState(
            infoName,
            infoSurname,
            infoNumber,
            lpCount,
            lpLevel,
            tokensCount,
            job,
            canIssue,
            canViewDetails,
            comp.IssueCard != null,
            generatedNumber,
            records,
            selectedDetails,
            canViewDetails,
            denunciations,
            selectedDenunciation);

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

    private static string FormatDelta(int delta)
    {
        return delta >= 0 ? $"+{delta}" : delta.ToString();
    }

    private readonly record struct TrackedMainCardState(EntityUid CardUid, int LPCount, int TokensCount);

    private bool TryGetResolverCid(Entity<CIDTabletComponent> ent, out EntityUid resolverCid)
    {
        if (ent.Comp.MainCard is { } cardUid &&
            TryComp<CIDCardComponent>(cardUid, out var cid) &&
            cid.Access > 2)
        {
            resolverCid = cardUid;
            return true;
        }

        resolverCid = default;
        return false;
    }

    private (string name, string surname, string cNumber) GetCidIdentity(EntityUid cardUid)
    {
        if (!TryComp<CIDCardComponent>(cardUid, out var cid))
            return ("-", "-", "-");

        TryComp<IdCardComponent>(cardUid, out var idComp);
        var (name, surname) = SplitName(idComp?.FullName);
        var cNumber = string.IsNullOrWhiteSpace(cid.CNumber) ? "-" : cid.CNumber;
        return (name, surname, cNumber);
    }
}
