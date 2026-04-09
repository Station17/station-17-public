using Content.Server.DoAfter;
using Content.Server.Popups;
using Content.Server.HL2RP.Contracts.Components;
using Content.Shared.DoAfter;
using Content.Shared.Hands;
using Content.Shared.HL2RP.CID.Components;
using Content.Shared.HL2RP.Contracts.Components;
using Content.Shared.HL2RP.Contracts.DoAfter;
using Content.Shared.HL2RP.Contracts.Prototypes;
using Content.Shared.Interaction;
using Content.Shared.Interaction.Events;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;

namespace Content.Server.HL2RP.Contracts.Systems;

public sealed class CargoBoxContractSystem : EntitySystem
{
    [Dependency] private readonly DoAfterSystem _doAfter = default!;
    [Dependency] private readonly EntityLookupSystem _lookup = default!;
    [Dependency] private readonly IGameTiming _timing = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly PopupSystem _popup = default!;

    private readonly HashSet<Entity<CargoBoxContractItemComponent>> _nearbyBoxes = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CargoBoxSpawnerComponent, MapInitEvent>(OnSpawnerMapInit);
        SubscribeLocalEvent<CargoBoxDeliveryPointComponent, AfterInteractUsingEvent>(OnAfterInteractUsing);
        SubscribeLocalEvent<CargoBoxDeliveryPointComponent, CargoBoxDeliverDoAfterEvent>(OnDeliverDoAfter);

        SubscribeLocalEvent<CargoBoxContractItemComponent, MapInitEvent>(OnBoxMapInit);
        SubscribeLocalEvent<CargoBoxContractItemComponent, DroppedEvent>(OnBoxDropped);
        SubscribeLocalEvent<CargoBoxContractItemComponent, GotEquippedHandEvent>(OnBoxEquippedHand);
        SubscribeLocalEvent<CargoBoxContractItemComponent, EntInsertedIntoContainerMessage>(OnBoxInsertedContainer);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);
        var now = _timing.CurTime;

        var spawnerQuery = EntityQueryEnumerator<CargoBoxSpawnerComponent>();
        while (spawnerQuery.MoveNext(out var spawnerUid, out var spawner))
        {
            if (spawner.NextSpawnAt > now)
                continue;

            spawner.NextSpawnAt = now + TimeSpan.FromSeconds(spawner.SpawnIntervalSeconds);

            if (HasBoxOnSpawner(spawnerUid))
                continue;

            var boxUid = Spawn(spawner.BoxPrototype, Transform(spawnerUid).Coordinates);
            if (!TryComp<CargoBoxContractItemComponent>(boxUid, out var boxComp))
                continue;

            boxComp.Spawner = GetNetEntity(spawnerUid);
            boxComp.HighlightEnabled = HasAnyActiveWorker();
            Dirty(boxUid, boxComp);
        }

        var boxQuery = EntityQueryEnumerator<CargoBoxContractItemComponent, CargoBoxContractItemServerComponent>();
        while (boxQuery.MoveNext(out var boxUid, out var boxComp, out var serverComp))
        {
            if (serverComp.DeleteAt is not { } deleteAt || deleteAt > now)
                continue;

            if (!IsOnFloor(boxUid) || IsAtSpawner(boxUid, boxComp))
            {
                serverComp.DeleteAt = null;
                continue;
            }

            QueueDel(boxUid);
        }
    }

    public void UpdateBoxHighlights()
    {
        var enabled = HasAnyActiveWorker();
        var boxQuery = EntityQueryEnumerator<CargoBoxContractItemComponent>();
        while (boxQuery.MoveNext(out var boxUid, out var boxComp))
        {
            if (boxComp.HighlightEnabled == enabled)
                continue;

            boxComp.HighlightEnabled = enabled;
            Dirty(boxUid, boxComp);
        }
    }

    private void OnSpawnerMapInit(Entity<CargoBoxSpawnerComponent> ent, ref MapInitEvent args)
    {
        ent.Comp.NextSpawnAt = _timing.CurTime;
    }

    private void OnBoxMapInit(Entity<CargoBoxContractItemComponent> ent, ref MapInitEvent args)
    {
        EnsureComp<CargoBoxContractItemServerComponent>(ent.Owner);
        if (TryComp<CargoBoxContractItemServerComponent>(ent.Owner, out var serverComp))
        {
            serverComp.DeleteAt = null;
        }
    }

    private void OnBoxDropped(Entity<CargoBoxContractItemComponent> ent, ref DroppedEvent args)
    {
        if (!TryComp<CargoBoxContractItemServerComponent>(ent.Owner, out var serverComp))
            return;

        serverComp.DeleteAt = IsAtSpawner(ent.Owner, ent.Comp) ? null : _timing.CurTime + TimeSpan.FromMinutes(1);
    }

    private void OnBoxEquippedHand(Entity<CargoBoxContractItemComponent> ent, ref GotEquippedHandEvent args)
    {
        if (!TryComp<CargoBoxContractItemServerComponent>(ent.Owner, out var serverComp))
            return;

        serverComp.DeleteAt = null;
    }

    private void OnBoxInsertedContainer(Entity<CargoBoxContractItemComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (!TryComp<CargoBoxContractItemServerComponent>(ent.Owner, out var serverComp))
            return;

        serverComp.DeleteAt = null;
    }

    private void OnAfterInteractUsing(Entity<CargoBoxDeliveryPointComponent> ent, ref AfterInteractUsingEvent args)
    {
        if (args.Handled || !args.CanReach)
            return;

        if (!TryComp<CargoBoxContractItemComponent>(args.Used, out _))
            return;

        if (!TryComp<ActiveBasicContractComponent>(args.User, out var active) ||
            !_prototypes.TryIndex<BasicContractPrototype>(active.ContractId, out var proto) ||
            !string.Equals(proto.ObjectiveType, "CargoBoxes", StringComparison.Ordinal))
        {
            return;
        }

        args.Handled = true;
        var doAfter = new DoAfterArgs(EntityManager,
            args.User,
            ent.Comp.DeliverDoAfter,
            new CargoBoxDeliverDoAfterEvent(),
            ent.Owner,
            target: ent.Owner,
            used: args.Used)
        {
            BreakOnMove = false,
            BreakOnDamage = true,
            NeedHand = true,
            BreakOnDropItem = true,
            DistanceThreshold = 1.5f,
            Hidden = false,
            DuplicateCondition = DuplicateConditions.SameTarget,
        };

        _doAfter.TryStartDoAfter(doAfter);
    }

    private void OnDeliverDoAfter(Entity<CargoBoxDeliveryPointComponent> ent, ref CargoBoxDeliverDoAfterEvent args)
    {
        if (args.Cancelled || args.Handled || args.Args.Used is not { } boxUid)
            return;

        if (!TryComp<CargoBoxContractItemComponent>(boxUid, out _))
            return;

        if (!TryComp<ActiveBasicContractComponent>(args.Args.User, out var active) ||
            !_prototypes.TryIndex<BasicContractPrototype>(active.ContractId, out var proto) ||
            !string.Equals(proto.ObjectiveType, "CargoBoxes", StringComparison.Ordinal))
        {
            return;
        }

        if (!Deleted(boxUid))
            QueueDel(boxUid);

        active.Progress = Math.Clamp(active.Progress + 1, 0, active.RequiredCount);
        Dirty(args.Args.User, active);

        if (active.Progress >= active.RequiredCount)
        {
            if (TryFindUsersCid(args.Args.User, out var cidUid, out var cid))
            {
                cid.LPCount = Math.Clamp(cid.LPCount + proto.RewardLp, -9999, 9999);
                cid.TokensCount = Math.Clamp(cid.TokensCount + proto.RewardTokens, -999999, 999999);
                Dirty(cidUid, cid);
            }

            RemComp<CargoBoxContractWorkerComponent>(args.Args.User);
            RemComp(args.Args.User, active);
            UpdateBoxHighlights();
            _popup.PopupEntity(Loc.GetString("hl2rp-contracts-cargo-complete",
                ("lp", proto.RewardLp),
                ("tokens", proto.RewardTokens)), args.Args.User, args.Args.User, PopupType.Medium);
        }

        args.Handled = true;
    }

    private bool HasBoxOnSpawner(EntityUid spawnerUid)
    {
        var spawnerCoords = Transform(spawnerUid).Coordinates;
        _nearbyBoxes.Clear();
        _lookup.GetEntitiesInRange(spawnerCoords, 0.2f, _nearbyBoxes);
        foreach (var boxEnt in _nearbyBoxes)
        {
            if (boxEnt.Comp.Spawner == GetNetEntity(spawnerUid))
                return true;
        }

        return false;
    }

    private bool IsAtSpawner(EntityUid boxUid, CargoBoxContractItemComponent boxComp)
    {
        var spawnerUid = GetEntity(boxComp.Spawner);
        if (spawnerUid == EntityUid.Invalid || !Exists(spawnerUid))
            return false;

        var boxPos = Transform(boxUid).Coordinates.Position;
        var spawnerPos = Transform(spawnerUid).Coordinates.Position;
        return (boxPos - spawnerPos).LengthSquared() <= 0.04f;
    }

    private bool IsOnFloor(EntityUid uid)
    {
        var xform = Transform(uid);
        if (!xform.ParentUid.IsValid())
            return false;

        return xform.ParentUid == xform.GridUid || xform.ParentUid == xform.MapUid;
    }

    private bool HasAnyActiveWorker()
    {
        var workerQuery = EntityQueryEnumerator<CargoBoxContractWorkerComponent>();
        return workerQuery.MoveNext(out _, out _);
    }

    private bool TryFindUsersCid(EntityUid user, out EntityUid cidUid, out CIDCardComponent cid)
    {
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
}
