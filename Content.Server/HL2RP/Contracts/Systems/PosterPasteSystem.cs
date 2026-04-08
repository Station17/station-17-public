using Content.Shared.DoAfter;
using Content.Shared.HL2RP.Contracts.Components;
using Content.Shared.HL2RP.Contracts.DoAfter;
using Content.Shared.Interaction;
using Content.Shared.Popups;
using Content.Shared.EntityTable;
using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.Inventory;
using Content.Server.Stack;
using Content.Shared.HL2RP.CID.Components;
using Robust.Shared.Map;
using Robust.Shared.Prototypes;
using Robust.Shared.Random;
using Robust.Shared.Timing;
using System.Linq;

namespace Content.Server.HL2RP.Contracts.Systems;

public sealed class PosterPasteSystem : EntitySystem
{
    private static readonly ProtoId<EntityTablePrototype> ContrabandTable = "ContrabandPosterTable";
    private static readonly ProtoId<EntityTablePrototype> LegitTable = "LegitPosterTable";

    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly EntityTableSystem _tables = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IRobustRandom _random = default!;
    [Dependency] private readonly ServerInventorySystem _inventory = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly StackSystem _stacks = default!;

    // Track spawned poster -> marker for reactivation on deletion.
    private readonly Dictionary<EntityUid, EntityUid> _posterToMarker = new();
    private readonly HashSet<Entity<PosterPasteMarkerComponent>> _nearbyMarkers = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContractLeafletComponent, AfterInteractEvent>(OnAfterInteract);
        SubscribeLocalEvent<ContractLeafletComponent, PosterPasteDoAfterEvent>(OnDoAfter);

        SubscribeLocalEvent<PosterPasteMarkerComponent, EntityTerminatingEvent>(OnMarkerTerminating);
    }

    private void OnAfterInteract(Entity<ContractLeafletComponent> ent, ref AfterInteractEvent args)
    {
        if (args.Handled)
            return;

        if (!args.CanReach || args.Target is not { } target)
            return;

        // Only workers with an active contract can paste.
        if (!HasComp<PosterPasteContractWorkerComponent>(args.User) ||
            !HasComp<ActiveBasicContractComponent>(args.User))
        {
            return;
        }

        // You click a wall, not the hidden marker entity. Find an active marker near the click location.
        _nearbyMarkers.Clear();
        _lookup.GetEntitiesInRange(args.ClickLocation, 0.6f, _nearbyMarkers);
        EntityUid? markerUid = null;
        foreach (var markerEnt in _nearbyMarkers)
        {
            if (!markerEnt.Comp.Active)
                continue;

            markerUid = markerEnt.Owner;
            break;
        }

        if (markerUid == null)
            return;

        args.Handled = true;

        var doAfter = new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.PasteDoAfter,
            new PosterPasteDoAfterEvent(GetNetEntity(markerUid.Value)),
            ent.Owner,
            target: target,
            used: ent.Owner)
        {
            BreakOnMove = true,
            MovementThreshold = 0.01f,
            BreakOnDamage = true,
            NeedHand = true,
            Hidden = false, // visible to everyone
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        if (!_doAfter.TryStartDoAfter(doAfter))
            return;
    }

    private void OnDoAfter(Entity<ContractLeafletComponent> ent, ref PosterPasteDoAfterEvent args)
    {
        if (args.Handled || args.Cancelled)
            return;

        var markerUid = GetEntity(args.Marker);
        if (markerUid == EntityUid.Invalid || !Exists(markerUid))
            return;

        if (!TryComp<PosterPasteMarkerComponent>(markerUid, out var marker) || !marker.Active)
            return;

        if (args.Args.Target is not { } wallUid)
            return;

        // Must have active contract.
        if (!TryComp<ActiveBasicContractComponent>(args.Args.User, out var active))
            return;

        // Consume leaflet.
        if (TryComp<ContractGrantedItemComponent>(ent.Owner, out var granted))
        {
            // If leaflet isn't from the active contract, don't allow progress.
            if (!string.Equals(granted.ContractId, active.ContractId, StringComparison.Ordinal))
                return;

            // Also ensure this leaflet belongs to this user (prevents using someone else's stack).
            if (granted.GrantedTo != GetNetEntity(args.Args.User))
                return;
        }

        // Consume 1 leaflet (stack-aware).
        if (TryComp<Content.Shared.Stacks.StackComponent>(ent.Owner, out var stack))
        {
            if (!_stacks.TryUse((ent.Owner, stack), 1))
                return;
        }
        else
        {
            if (!Deleted(ent.Owner))
                QueueDel(ent.Owner);
        }

        // Deactivate marker.
        marker.Active = false;
        Dirty(markerUid, marker);

        // Spawn poster and schedule deletion + reactivation.
        var poster = SpawnRandomPoster(Transform(wallUid).Coordinates);
        if (poster != null && Exists(poster.Value))
        {
            Timer.Spawn(TimeSpan.FromMinutes(5), () =>
            {
                if (Exists(poster.Value))
                    QueueDel(poster.Value);

                if (!Exists(markerUid) || !TryComp(markerUid, out PosterPasteMarkerComponent? m))
                    return;

                m.Active = true;
                Dirty(markerUid, m);
            });
        }
        else
        {
            // If we failed to spawn a poster, reactivate quickly so marker doesn't get stuck.
            Timer.Spawn(TimeSpan.FromSeconds(1), () =>
            {
                if (!Exists(markerUid) || !TryComp(markerUid, out PosterPasteMarkerComponent? m))
                    return;
                m.Active = true;
                Dirty(markerUid, m);
            });
        }

        // Progress + completion reward.
        active.Progress = Math.Clamp(active.Progress + 1, 0, active.RequiredCount);
        Dirty(args.Args.User, active);

        if (active.Progress >= active.RequiredCount)
        {
            if (TryFindUsersCid(args.Args.User, out var cidUid, out var cid))
            {
                cid.LPCount = Math.Clamp(cid.LPCount + 1, -9999, 9999);
                cid.TokensCount = Math.Clamp(cid.TokensCount + 150, -999999, 999999);
                Dirty(cidUid, cid);
            }

            RemComp<PosterPasteContractWorkerComponent>(args.Args.User);
            RemComp(args.Args.User, active);
            _popup.PopupEntity(Loc.GetString("hl2rp-contracts-poster-complete"), args.Args.User, args.Args.User, PopupType.Medium);
        }

        args.Handled = true;
    }

    private bool TryFindUsersCid(EntityUid user, out EntityUid cidUid, out CIDCardComponent cid)
    {
        // Prefer CID inserted into any CID tablet the user is carrying/wearing.
        var tabletQuery = EntityQueryEnumerator<CIDTabletComponent>();
        while (tabletQuery.MoveNext(out var tabletUid, out var tablet))
        {
            if (!IsInHierarchy(tabletUid, user))
                continue;

        if (tablet.MainCard is { } main && TryComp<CIDCardComponent>(main, out var found))
            {
                cidUid = main;
            cid = found;
                return true;
            }
        }

        // Otherwise, any CID card in the user's hierarchy.
        var cidQuery = EntityQueryEnumerator<CIDCardComponent>();
        while (cidQuery.MoveNext(out var foundUid, out var found))
        {
            if (!IsInHierarchy(foundUid, user))
                continue;

            cidUid = foundUid;
            cid = found;
            return true;
        }

        cidUid = default;
        cid = default!;
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

    private EntityUid? SpawnRandomPoster(EntityCoordinates coords)
    {
        // Mimic RandomPosterAny loosely: small chance for broken, otherwise pick between legit/contraband.
        if (_random.Prob(0.05f))
            return Spawn("PosterBroken", coords);

        if (!_prototypes.TryIndex(ContrabandTable, out var contraband) ||
            !_prototypes.TryIndex(LegitTable, out var legit))
            return Spawn("PosterBroken", coords);

        var pickContraband = _random.Prob(0.5f);
        var table = pickContraband ? contraband : legit;
        var spawns = _tables.GetSpawns(table).ToList();
        if (spawns.Count == 0)
            return Spawn("PosterBroken", coords);

        var proto = _random.Pick(spawns);
        if (string.IsNullOrWhiteSpace(proto))
            return Spawn("PosterBroken", coords);

        return Spawn(proto, coords);
    }

    private void OnMarkerTerminating(Entity<PosterPasteMarkerComponent> ent, ref EntityTerminatingEvent args)
    {
        // If marker is deleted, just clear any tracked links.
        foreach (var (poster, marker) in _posterToMarker.ToArray())
        {
            if (marker == ent.Owner)
                _posterToMarker.Remove(poster);
        }
    }

    // Marker reactivation is handled by the scheduled timer when the poster despawns.
}

