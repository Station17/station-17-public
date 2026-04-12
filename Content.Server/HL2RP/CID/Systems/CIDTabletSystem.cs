using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using Content.Server.HL2RP.CID.Services;
using Content.Server.HL2RP.Denunciations.Systems;
using Content.Server.Mind;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Server.Roles.Jobs;
using Content.Server.Station.Components;
using Content.Server.Station.Systems;
using Content.Shared.Access.Components;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.HL2RP.CID;
using Content.Shared.HL2RP.CID.Components;
using Content.Shared.HL2RP.CID.Systems;
using Content.Shared.HL2RP.CID.UI;
using Content.Shared.Mind.Components;
using Content.Shared.Popups;
using Content.Shared.Roles;
using Content.Shared.Roles.Jobs;
using Content.Shared.Station;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;
using Robust.Shared.Utility;

namespace Content.Server.HL2RP.CID.Systems;

public sealed class CIDTabletSystem : SharedCIDTabletSystem
{
    /// <summary>Jobs the CID tablet must not assign (non-human / special roles).</summary>
    private static readonly HashSet<string> TabletJobChangeDisallowedTargets = new(StringComparer.Ordinal)
    {
        "VortigauntSlave",
        "VortigauntFree",
    };

    private static readonly Regex NumberRegex = new("^[0-9]{6}$", RegexOptions.Compiled);
    private const float GlobalUiRefreshInterval = 1.0f;
    private static readonly SoundSpecifier NotifyBeep = new SoundPathSpecifier("/Audio/Machines/scan_finish.ogg");

    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly DenunciationsSystem _denunciations = default!;
    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly MindSystem _mind = default!;
    [Dependency] private readonly RoleSystem _roles = default!;
    [Dependency] private readonly JobSystem _jobs = default!;
    [Dependency] private readonly StationJobsSystem _stationJobs = default!;
    [Dependency] private readonly SharedStationSystem _station = default!;
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
        SubscribeLocalEvent<CIDTabletComponent, CIDChangeCitizenJobMessage>(OnChangeCitizenJob);
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

        if (!mainCid.TabletPermissions.HasFlag(CidTabletPermissions.EditLoyaltyPoints)
            || !mainCid.TabletPermissions.HasFlag(CidTabletPermissions.ViewExtendedCitizenInfo))
            return;

        var target = GetEntity(args.CardUid);
        if (!TryComp<CIDCardComponent>(target, out var cid))
            return;

        cid.LPCount = Math.Clamp(args.LPCount, -9999, 9999);
        Dirty(target, cid);
        RefreshAllOpenTabletUis();
    }

    private void OnChangeCitizenJob(Entity<CIDTabletComponent> ent, ref CIDChangeCitizenJobMessage args)
    {
        if (!TryComp<CIDCardComponent>(ent.Comp.MainCard, out var mainCid)
            || !mainCid.TabletPermissions.HasFlag(CidTabletPermissions.ViewExtendedCitizenInfo))
            return;

        var canAll = mainCid.TabletPermissions.HasFlag(CidTabletPermissions.ChangeJob);
        var canDept = mainCid.TabletPermissions.HasFlag(CidTabletPermissions.ChangeJobDepartment);
        if (!canAll && !canDept)
            return;

        if (_selectedCards.GetValueOrDefault(ent.Owner) != GetEntity(args.CardUid))
            return;

        var targetCard = GetEntity(args.CardUid);
        if (ent.Comp.MainCard == targetCard)
            return;

        if (!TryChangeCitizenJob(targetCard, args.NewJobId, canAll, canDept))
            return;

        RefreshAllOpenTabletUis();
    }

    private bool TryChangeCitizenJob(EntityUid targetCard, ProtoId<JobPrototype> newJobId, bool canAll, bool canDept)
    {
        if (!TryFindWearerMob(targetCard, out var wearer))
            return false;

        if (!_mind.TryGetMind(wearer, out var mindId, out var mind) || mind.UserId is not { } userId)
            return false;

        if (!_jobs.MindTryGetJobId(mindId, out var currentJobId) || currentJobId is not { } curJobId)
            return false;

        if (curJobId == newJobId)
            return false;

        if (!_prototype.TryIndex(newJobId, out var newJobProto) || !newJobProto.SetPreference)
            return false;

        if (TabletJobChangeDisallowedTargets.Contains(newJobId.Id))
            return false;

        if (!canAll && (!canDept || !JobsShareDepartment(curJobId.Id, newJobId.Id)))
            return false;

        var station = _station.GetOwningStation(wearer);
        if (station is not { } stationUid || !TryComp<StationJobsComponent>(stationUid, out var stationJobs))
            return false;

        _stationJobs.EnsurePlayerJobsList(stationUid, userId, stationJobs);

        var oldId = curJobId.Id;
        if (!_stationJobs.TryAdjustJobSlot(stationUid, oldId, 1, clamp: true))
            return false;

        _stationJobs.RemoveJobFromPlayerJobsList(stationUid, userId, oldId, stationJobs);

        if (!_stationJobs.TryAssignJob(stationUid, newJobId, userId))
        {
            _stationJobs.TryAdjustJobSlot(stationUid, oldId, -1, clamp: true);
            _stationJobs.AddJobToPlayerJobsList(stationUid, userId, new ProtoId<JobPrototype>(oldId), stationJobs);
            return false;
        }

        _roles.MindAddJobRole(mindId, mind, silent: true, newJobId.Id);

        if (TryComp<CIDCardComponent>(targetCard, out var cid))
        {
            cid.Job = newJobProto.LocalizedName;
            Dirty(targetCard, cid);
        }

        if (TryComp<IdCardComponent>(targetCard, out var idCard))
        {
            idCard.LocalizedJobTitle = newJobProto.LocalizedName;
            Dirty(targetCard, idCard);
        }

        return true;
    }

    private bool TryFindWearerMob(EntityUid uid, out EntityUid mobUid)
    {
        var current = uid;
        while (current.IsValid())
        {
            if (TryComp<MindContainerComponent>(current, out var mc) && mc.HasMind)
            {
                mobUid = current;
                return true;
            }

            var parent = Transform(current).ParentUid;
            if (!parent.IsValid())
                break;
            current = parent;
        }

        mobUid = default;
        return false;
    }

    private bool JobsShareDepartment(string jobA, string jobB)
    {
        if (!_jobs.TryGetAllDepartments(jobA, out var depsA) || depsA.Count == 0)
            return false;
        if (!_jobs.TryGetAllDepartments(jobB, out var depsB) || depsB.Count == 0)
            return false;

        foreach (var da in depsA)
        {
            foreach (var db in depsB)
            {
                if (da.ID == db.ID)
                    return true;
            }
        }

        return false;
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

        if (!mainCid.TabletPermissions.HasFlag(CidTabletPermissions.IssueCards) || ent.Comp.IssueCard is not { } issueUid)
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
        issueCid.TabletPermissions = CidTabletPermissions.None;
        issueCid.Job = "Без должности";
        issueCid.IsBlank = false;
        issueCid.FirstName = name;
        issueCid.LastName = surname;
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

        var mainPerms = CidTabletPermissions.None;
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
            mainPerms = mainCid.TabletPermissions;
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

        var canIssueCards = mainPerms.HasFlag(CidTabletPermissions.IssueCards);
        var canViewExtendedCitizenInfo = mainPerms.HasFlag(CidTabletPermissions.ViewExtendedCitizenInfo);
        var canEditLoyaltyPoints = mainPerms.HasFlag(CidTabletPermissions.EditLoyaltyPoints);
        var canUseDenunciations = mainPerms.HasFlag(CidTabletPermissions.Denunciations);

        var records = new List<CIDDatabaseRecord>();
        CIDRecordDetails? selectedDetails = null;
        var selectedUid = _selectedCards.GetValueOrDefault(uid);

        var query = EntityQueryEnumerator<CIDCardComponent>();
        while (query.MoveNext(out var cardUid, out var cid))
        {
            if (cid.IsBlank)
                continue;

            TryComp<IdCardComponent>(cardUid, out var id);
            var (name, surname) = SplitName(id?.FullName);
            records.Add(new CIDDatabaseRecord(GetNetEntity(cardUid), name, surname, cid.CNumber));

            if (canViewExtendedCitizenInfo && selectedUid == cardUid)
            {
                selectedDetails = new CIDRecordDetails(
                    GetNetEntity(cardUid),
                    name,
                    surname,
                    cid.CNumber,
                    cid.LPCount,
                    cid.LPLevel,
                    cid.TokensCount,
                    cid.TabletPermissions,
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
        if (canUseDenunciations)
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
                var canTake = canUseDenunciations && denunciation.ResolverCard == null;
                var canControl = canUseDenunciations && comp.MainCard != null && denunciation.ResolverCard == comp.MainCard;

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

        var jobChangeOptions = new List<CIDJobPickerEntry>();
        var isOperatorsOwnRecord = comp.MainCard is { } mainSlotCard
            && selectedUid is { } selUid
            && selUid == mainSlotCard;

        if (canViewExtendedCitizenInfo
            && !isOperatorsOwnRecord
            && (mainPerms.HasFlag(CidTabletPermissions.ChangeJob)
                || mainPerms.HasFlag(CidTabletPermissions.ChangeJobDepartment))
            && selectedUid is { } selCard
            && Exists(selCard))
        {
            var canPickAll = mainPerms.HasFlag(CidTabletPermissions.ChangeJob);
            var canPickDept = mainPerms.HasFlag(CidTabletPermissions.ChangeJobDepartment);
            if (TryFindWearerMob(selCard, out var wearer)
                && _mind.TryGetMind(wearer, out var mindEnt, out var mindComp)
                && mindComp.UserId != null
                && _jobs.MindTryGetJobId(mindEnt, out var curJob)
                && curJob is { } curJobId)
            {
                foreach (var jobProto in _prototype.EnumeratePrototypes<JobPrototype>().OrderBy(j => j.LocalizedName))
                {
                    if (!jobProto.SetPreference)
                        continue;
                    if (TabletJobChangeDisallowedTargets.Contains(jobProto.ID))
                        continue;
                    if (jobProto.ID == curJobId.Id)
                        continue;
                    if (!canPickAll && (!canPickDept || !JobsShareDepartment(curJobId.Id, jobProto.ID)))
                        continue;

                    var title = CultureInfo.CurrentCulture.TextInfo.ToTitleCase(jobProto.LocalizedName);
                    jobChangeOptions.Add(new CIDJobPickerEntry(jobProto.ID, title));
                }
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
            canIssueCards,
            canViewExtendedCitizenInfo,
            canEditLoyaltyPoints,
            comp.IssueCard != null,
            generatedNumber,
            records,
            selectedDetails,
            canUseDenunciations,
            denunciations,
            selectedDenunciation,
            jobChangeOptions);

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
            cid.TabletPermissions.HasFlag(CidTabletPermissions.Denunciations))
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
