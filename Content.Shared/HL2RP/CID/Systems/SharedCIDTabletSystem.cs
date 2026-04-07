using Content.Shared.Containers.ItemSlots;
using Content.Shared.HL2RP.CID.Components;
using Robust.Shared.Containers;

namespace Content.Shared.HL2RP.CID.Systems;

public abstract class SharedCIDTabletSystem : EntitySystem
{
    [Dependency] protected readonly ItemSlotsSystem ItemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CIDTabletComponent, ComponentInit>(OnComponentInit);
        SubscribeLocalEvent<CIDTabletComponent, ComponentRemove>(OnComponentRemove);
        SubscribeLocalEvent<CIDTabletComponent, EntInsertedIntoContainerMessage>(OnItemInserted);
        SubscribeLocalEvent<CIDTabletComponent, EntRemovedFromContainerMessage>(OnItemRemoved);
    }

    private void OnComponentInit(EntityUid uid, CIDTabletComponent comp, ComponentInit args)
    {
        ItemSlots.AddItemSlot(uid, CIDTabletComponent.MainCardSlotId, comp.MainCardSlot);
        ItemSlots.AddItemSlot(uid, CIDTabletComponent.IssueCardSlotId, comp.IssueCardSlot);
        comp.MainCard = comp.MainCardSlot.Item;
        comp.IssueCard = comp.IssueCardSlot.Item;
    }

    private void OnComponentRemove(EntityUid uid, CIDTabletComponent comp, ComponentRemove args)
    {
        ItemSlots.RemoveItemSlot(uid, comp.MainCardSlot);
        ItemSlots.RemoveItemSlot(uid, comp.IssueCardSlot);
    }

    private void OnItemInserted(EntityUid uid, CIDTabletComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == CIDTabletComponent.MainCardSlotId)
            comp.MainCard = args.Entity;
        else if (args.Container.ID == CIDTabletComponent.IssueCardSlotId)
            comp.IssueCard = args.Entity;
    }

    private void OnItemRemoved(EntityUid uid, CIDTabletComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == CIDTabletComponent.MainCardSlotId)
            comp.MainCard = null;
        else if (args.Container.ID == CIDTabletComponent.IssueCardSlotId)
            comp.IssueCard = null;
    }
}
