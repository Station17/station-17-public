using Content.Shared.Examine;
using Content.Shared.HL2RP.CharacterPersistence.Components;

namespace Content.Server.HL2RP.CharacterPersistence;

public sealed class UnSaveableExamineSystem : EntitySystem
{
    public override void Initialize()
    {
        SubscribeLocalEvent<UnSaveableComponent, ExaminedEvent>(OnExamined);
    }

    private void OnExamined(EntityUid uid, UnSaveableComponent component, ExaminedEvent args)
    {
        args.PushMarkup(Loc.GetString("hl2rp-unsaveable-examine"));
    }
}
