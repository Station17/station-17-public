using Content.Shared.HL2RP.CID.Components;

namespace Content.Server.HL2RP.CID.Systems;

public sealed class CIDCardSystem : EntitySystem
{
    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<CIDCardComponent, MapInitEvent>(OnMapInit);
    }

    private void OnMapInit(Entity<CIDCardComponent> ent, ref MapInitEvent args)
    {
        if (string.IsNullOrWhiteSpace(ent.Comp.CNumber))
            ent.Comp.IsBlank = true;

        if (ent.Comp.ApplyLegacyAccessIfPresent())
            Dirty(ent);
    }
}
