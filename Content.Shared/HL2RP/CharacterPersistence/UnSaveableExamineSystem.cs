using Content.Shared.Examine;

namespace Content.Shared.HL2RP.CharacterPersistence;

// HL2RP CHANGE START: examine hint for non-persistable items.
public sealed class UnSaveableExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<UnSaveableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(Entity<UnSaveableComponent> ent, ref ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("hl2rp-unsaveable-examine"), -5);
    }
}
// HL2RP CHANGE END
