using Content.Server.Popups;
using Content.Server.Inventory;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Access.Components;
using Content.Shared.HL2RP.CID.Components;
using Content.Shared.HL2RP.Contracts.Components;
using Content.Shared.HL2RP.Contracts.Prototypes;
using Content.Shared.HL2RP.Contracts.Systems;
using Content.Shared.HL2RP.Contracts.UI;
using Content.Shared.Popups;
using Content.Shared.UserInterface;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.HL2RP.Contracts.Systems;

public sealed class ContractsTerminalSystem : SharedContractsTerminalSystem
{
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly ServerInventorySystem _inventory = default!;
    [Dependency] private readonly SharedHandsSystem _hands = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContractsTerminalComponent, BoundUIOpenedEvent>(OnUiOpened);

        Subs.BuiEvents<ContractsTerminalComponent>(ContractsTerminalUiKey.Key, subs =>
        {
            subs.Event<ContractsAcceptMessage>(OnAccept);
            subs.Event<ContractsCancelMessage>(OnCancel);
        });

        SubscribeLocalEvent<ContractsTerminalComponent, ItemSlotEjectAttemptEvent>(OnEjectAttempt);
    }

    private void OnUiOpened(Entity<ContractsTerminalComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent.Owner, ent.Comp, args.Actor);
    }

    protected override void OnInserted(EntityUid uid, ContractsTerminalComponent comp, EntInsertedIntoContainerMessage args)
    {
        base.OnInserted(uid, comp, args);

        if (_ui.IsUiOpen(uid, ContractsTerminalUiKey.Key))
            UpdateUi(uid, comp);
    }

    protected override void OnRemoved(EntityUid uid, ContractsTerminalComponent comp, EntRemovedFromContainerMessage args)
    {
        base.OnRemoved(uid, comp, args);

        if (_ui.IsUiOpen(uid, ContractsTerminalUiKey.Key))
            UpdateUi(uid, comp);
    }

    private void OnAccept(Entity<ContractsTerminalComponent> ent, ref ContractsAcceptMessage args)
    {
        if (args.Actor is not { Valid: true } user)
            return;

        // Terminal requires CID card inserted to accept.
        if (ent.Comp.InsertedCard is not { } cardUid || !TryComp<CIDCardComponent>(cardUid, out _))
        {
            _popup.PopupEntity(Loc.GetString("hl2rp-contracts-terminal-insert-card"), ent.Owner, user, PopupType.Medium);
            UpdateUi(ent.Owner, ent.Comp);
            return;
        }

        // Don't allow accepting a second contract while one is active.
        if (HasComp<ActiveBasicContractComponent>(user))
        {
            _popup.PopupEntity(Loc.GetString("hl2rp-contracts-terminal-already-has-contract"), ent.Owner, user, PopupType.Medium);
            UpdateUi(ent.Owner, ent.Comp);
            return;
        }

        if (!_prototypes.TryIndex<BasicContractPrototype>(args.ContractId, out var proto))
            return;

        EnsureComp<ActiveBasicContractComponent>(user, out var active);
        active.ContractId = proto.ID;
        active.Progress = 0;
        active.RequiredCount = proto.RequiredCount;
        active.AcceptedAt = _timing.CurTime;
        Dirty(user, active);

        EnsureComp<PosterPasteContractWorkerComponent>(user);

        // Give leaflets (drops if no space).
        var coords = Transform(user).Coordinates;
        for (var i = 0; i < proto.ItemCount; i++)
        {
            var leaflet = Spawn(proto.ItemToGive, coords);
            EnsureComp<ContractGrantedItemComponent>(leaflet).ContractId = proto.ID;
            _hands.PickupOrDrop(user, leaflet);
        }

        _popup.PopupEntity(Loc.GetString("hl2rp-contracts-terminal-accepted", ("title", proto.Title)), ent.Owner, user, PopupType.Medium);
        UpdateUi(ent.Owner, ent.Comp, user);
    }

    private void OnCancel(Entity<ContractsTerminalComponent> ent, ref ContractsCancelMessage args)
    {
        if (args.Actor is not { Valid: true } user)
            return;

        if (!TryComp<ActiveBasicContractComponent>(user, out var active) ||
            !_prototypes.TryIndex<BasicContractPrototype>(active.ContractId, out var proto))
        {
            UpdateUi(ent.Owner, ent.Comp, user);
            return;
        }

        // Remove granted items for this contract (wherever they are in the user's inventory).
        var grantedQuery = EntityQueryEnumerator<ContractGrantedItemComponent, TransformComponent>();
        while (grantedQuery.MoveNext(out var itemUid, out var granted, out var xform))
        {
            if (!string.Equals(granted.ContractId, proto.ID, StringComparison.Ordinal))
                continue;

            if (!IsInHierarchy(itemUid, user))
                continue;

            QueueDel(itemUid);
        }

        // Apply cancel penalty to user's CID card (id-slot).
        ApplyCidDelta(user, -proto.CancelPenaltyLp, -proto.CancelPenaltyTokens);

        RemComp<PosterPasteContractWorkerComponent>(user);
        RemComp(user, active);

        _popup.PopupEntity(Loc.GetString("hl2rp-contracts-terminal-cancelled"), ent.Owner, user, PopupType.Medium);
        UpdateUi(ent.Owner, ent.Comp, user);
    }

    private void UpdateUi(EntityUid terminalUid, ContractsTerminalComponent comp, EntityUid? viewer = null)
    {
        if (!_ui.HasUi(terminalUid, ContractsTerminalUiKey.Key))
            return;

        var hasCard = false;
        var lp = 0;
        var tokens = 0;
        if (comp.InsertedCard is { } cardUid && TryComp<CIDCardComponent>(cardUid, out var cid))
        {
            hasCard = true;
            lp = cid.LPCount;
            tokens = cid.TokensCount;
        }

        string? activeId = null;
        string? activeTitle = null;
        var activeProgress = 0;
        var activeRequired = 0;
        var cancelPenaltyLp = 1;
        var cancelPenaltyTokens = 50;

        if (viewer != null && TryComp<ActiveBasicContractComponent>(viewer, out var active))
        {
            activeId = active.ContractId;
            activeProgress = active.Progress;
            activeRequired = active.RequiredCount;
            if (_prototypes.TryIndex<BasicContractPrototype>(active.ContractId, out var p))
            {
                activeTitle = p.Title;
                cancelPenaltyLp = p.CancelPenaltyLp;
                cancelPenaltyTokens = p.CancelPenaltyTokens;
            }
        }

        var contracts = new List<ContractListEntry>();
        foreach (var proto in _prototypes.EnumeratePrototypes<BasicContractPrototype>())
        {
            contracts.Add(new ContractListEntry(
                proto.ID,
                proto.Title,
                proto.RequiredCount,
                proto.RewardLp,
                proto.RewardTokens));
        }

        contracts.Sort((a, b) => string.Compare(a.Title, b.Title, StringComparison.Ordinal));

        var state = new ContractsTerminalBoundUiState(
            hasCard,
            lp,
            tokens,
            activeId,
            activeTitle,
            activeProgress,
            activeRequired,
            cancelPenaltyLp,
            cancelPenaltyTokens,
            contracts);

        _ui.SetUiState(terminalUid, ContractsTerminalUiKey.Key, state);
    }

    private void OnEjectAttempt(Entity<ContractsTerminalComponent> ent, ref ItemSlotEjectAttemptEvent args)
    {
        // Only apply to our slot.
        if (args.Slot.ID != ContractsTerminalComponent.CardSlotId)
            return;

        // If no one is using terminal UI, allow.
        if (!_ui.IsUiOpen(ent.Owner, ContractsTerminalUiKey.Key))
            return;

        // If no user context, block to be safe.
        if (args.User is not { } user)
        {
            args.Cancelled = true;
            return;
        }

        // While UI is open, only the CID owner can eject.
        if (IsCardOwner(user, ent.Comp.InsertedCard))
            return;

        args.Cancelled = true;
        _popup.PopupEntity(Loc.GetString("hl2rp-contracts-terminal-card-locked"), ent.Owner, user, PopupType.Medium);
    }

    private bool IsCardOwner(EntityUid user, EntityUid? insertedCard)
    {
        if (insertedCard is not { } cardUid)
            return false;

        if (!TryComp<IdCardComponent>(cardUid, out var cardId) || string.IsNullOrWhiteSpace(cardId.FullName))
            return false;

        if (!_inventory.TryGetSlotEntity(user, "id", out var userIdUid) || userIdUid is not { } userIdCardUid)
            return false;

        if (!TryComp<IdCardComponent>(userIdCardUid, out var userId) || string.IsNullOrWhiteSpace(userId.FullName))
            return false;

        // Avoid calling methods like Trim() on the fields to satisfy analyzer permissions.
        return string.Equals(cardId.FullName, userId.FullName, StringComparison.OrdinalIgnoreCase);
    }

    private void ApplyCidDelta(EntityUid user, int deltaLp, int deltaTokens)
    {
        if (!_inventory.TryGetSlotEntity(user, "id", out var idUid) || idUid is not { } id)
            return;

        if (!TryComp<CIDCardComponent>(id, out var cid))
            return;

        cid.LPCount = Math.Clamp(cid.LPCount + deltaLp, -9999, 9999);
        cid.TokensCount = Math.Clamp(cid.TokensCount + deltaTokens, -999999, 999999);
        Dirty(id, cid);
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
}

