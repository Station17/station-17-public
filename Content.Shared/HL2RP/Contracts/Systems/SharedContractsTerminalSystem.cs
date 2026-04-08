using Content.Shared.Containers.ItemSlots;
using Content.Shared.HL2RP.Contracts.Components;
using Robust.Shared.Containers;

namespace Content.Shared.HL2RP.Contracts.Systems;

public abstract class SharedContractsTerminalSystem : EntitySystem
{
    [Dependency] protected readonly ItemSlotsSystem ItemSlots = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<ContractsTerminalComponent, ComponentInit>(OnInit);
        SubscribeLocalEvent<ContractsTerminalComponent, ComponentRemove>(OnRemove);
        SubscribeLocalEvent<ContractsTerminalComponent, EntInsertedIntoContainerMessage>(OnInserted);
        SubscribeLocalEvent<ContractsTerminalComponent, EntRemovedFromContainerMessage>(OnRemoved);
    }

    private void OnInit(EntityUid uid, ContractsTerminalComponent comp, ComponentInit args)
    {
        ItemSlots.AddItemSlot(uid, ContractsTerminalComponent.CardSlotId, comp.CardSlot);
        comp.InsertedCard = comp.CardSlot.Item;
    }

    private void OnRemove(EntityUid uid, ContractsTerminalComponent comp, ComponentRemove args)
    {
        ItemSlots.RemoveItemSlot(uid, comp.CardSlot);
    }

    protected virtual void OnInserted(EntityUid uid, ContractsTerminalComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == ContractsTerminalComponent.CardSlotId)
            comp.InsertedCard = args.Entity;
    }

    protected virtual void OnRemoved(EntityUid uid, ContractsTerminalComponent comp, EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == ContractsTerminalComponent.CardSlotId)
            comp.InsertedCard = null;
    }
}
