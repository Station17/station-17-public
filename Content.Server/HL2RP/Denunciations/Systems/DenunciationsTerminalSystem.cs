using Content.Server.HL2RP.Denunciations.Systems;
using Content.Server.Inventory;
using Content.Shared.Access.Components;
using Content.Shared.HL2RP.CID.Components;
using Content.Shared.HL2RP.Denunciations.Components;
using Content.Shared.HL2RP.Denunciations.UI;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;

namespace Content.Server.HL2RP.Denunciations.Systems;

public sealed class DenunciationsTerminalSystem : EntitySystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly ServerInventorySystem _inventory = default!;
    [Dependency] private readonly DenunciationsSystem _denunciations = default!;

    private readonly Dictionary<EntityUid, EntityUid?> _selectedCitizenByTerminal = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<DenunciationsTerminalComponent, BoundUIOpenedEvent>(OnUiOpened);

        Subs.BuiEvents<DenunciationsTerminalComponent>(DenunciationsTerminalUiKey.Key, subs =>
        {
            subs.Event<DenunciationsSelectCitizenMessage>(OnSelectCitizen);
            subs.Event<DenunciationsSubmitMessage>(OnSubmit);
        });

        _denunciations.ReportsChanged += RefreshAllOpenTerminalUis;
    }

    private void OnUiOpened(Entity<DenunciationsTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner, args.Actor);
    }

    private void OnSelectCitizen(Entity<DenunciationsTerminalComponent> ent, ref DenunciationsSelectCitizenMessage args)
    {
        var selectedUid = GetEntity(args.CardUid);
        _selectedCitizenByTerminal[ent.Owner] = selectedUid;
        UpdateUi(ent.Owner, args.Actor);
    }

    private void OnSubmit(Entity<DenunciationsTerminalComponent> ent, ref DenunciationsSubmitMessage args)
    {
        if (args.Actor is not { Valid: true } user)
            return;

        if (!TryFindUsersCid(user, out var reporterCid))
            return;

        var targetUid = GetEntity(args.TargetCardUid);
        _denunciations.Submit(reporterCid, targetUid, args.Reason, args.Severity);
        UpdateUi(ent.Owner, user);
    }

    private void RefreshAllOpenTerminalUis()
    {
        var query = EntityQueryEnumerator<DenunciationsTerminalComponent>();
        while (query.MoveNext(out var uid, out _))
        {
            if (!_ui.IsUiOpen(uid, DenunciationsTerminalUiKey.Key))
                continue;

            UpdateUi(uid);
        }
    }

    private void UpdateUi(EntityUid terminalUid, EntityUid? user = null)
    {
        if (!_ui.HasUi(terminalUid, DenunciationsTerminalUiKey.Key))
            return;

        var hasReporterCid = false;
        var reporterCNumber = "-";
        if (user is { } viewer &&
            TryFindUsersCid(viewer, out var reporterCid) &&
            TryComp<CIDCardComponent>(reporterCid, out var reporterCidComp))
        {
            hasReporterCid = true;
            reporterCNumber = string.IsNullOrWhiteSpace(reporterCidComp.CNumber) ? "-" : reporterCidComp.CNumber;
        }

        var citizens = new List<DenunciationCitizenEntry>();
        var selectedUid = _selectedCitizenByTerminal.GetValueOrDefault(terminalUid);
        DenunciationCitizenEntry? selected = null;

        var query = EntityQueryEnumerator<CIDCardComponent>();
        while (query.MoveNext(out var cardUid, out var cid))
        {
            if (cid.IsBlank)
                continue;

            TryComp<IdCardComponent>(cardUid, out var idComp);
            var (name, surname) = SplitName(idComp?.FullName);
            var entry = new DenunciationCitizenEntry(GetNetEntity(cardUid), name, surname, cid.CNumber);
            citizens.Add(entry);

            if (selectedUid == cardUid)
                selected = entry;
        }

        citizens.Sort((a, b) =>
            string.Compare($"{a.Name} {a.Surname}", $"{b.Name} {b.Surname}", StringComparison.Ordinal));

        var state = new DenunciationsTerminalBoundUiState(
            hasReporterCid,
            reporterCNumber,
            citizens,
            selected);
        _ui.SetUiState(terminalUid, DenunciationsTerminalUiKey.Key, state);
    }

    private bool TryFindUsersCid(EntityUid user, out EntityUid cidUid)
    {
        // Priority 1: CID in main slot of CID tablet located in id slot.
        if (_inventory.TryGetSlotEntity(user, "id", out var idEntity) && idEntity is { } idUid)
        {
            if (TryComp<CIDTabletComponent>(idUid, out var tablet) &&
                tablet.MainCard is { } mainCardUid &&
                HasComp<CIDCardComponent>(mainCardUid))
            {
                cidUid = mainCardUid;
                return true;
            }

            // Priority 2: direct CID in id slot.
            if (HasComp<CIDCardComponent>(idUid))
            {
                cidUid = idUid;
                return true;
            }
        }

        // Priority 3: any CID in the user's hierarchy/inventory.
        var cidQuery = EntityQueryEnumerator<CIDCardComponent>();
        while (cidQuery.MoveNext(out var foundUid, out _))
        {
            if (!IsInHierarchy(foundUid, user))
                continue;

            cidUid = foundUid;
            return true;
        }

        cidUid = default;
        return false;
    }

    private bool IsInHierarchy(EntityUid child, EntityUid potentialParent)
    {
        var xform = Transform(child);
        while (xform.ParentUid.IsValid())
        {
            if (xform.ParentUid == potentialParent)
                return true;
            xform = Transform(xform.ParentUid);
        }
        return false;
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
}
