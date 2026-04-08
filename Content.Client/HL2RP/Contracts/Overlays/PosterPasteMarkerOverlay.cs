using Content.Shared.HL2RP.Contracts.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Enums;
using Robust.Shared.Prototypes;
using Robust.Shared.Timing;
using Robust.Shared.Utility;
using System.Numerics;

namespace Content.Client.HL2RP.Contracts.Overlays;

public sealed class PosterPasteMarkerOverlay : Overlay
{
    private static readonly SpriteSpecifier.Rsi PaperRsi = new(new ResPath("/Textures/Objects/Misc/bureaucracy.rsi"), "paper");
    private static readonly ProtoId<ShaderPrototype> UnshadedShader = "unshaded";

    [Dependency] private readonly IEntityManager _entMan = default!;
    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly SpriteSystem _sprite;
    private readonly TransformSystem _transform;
    private readonly ShaderInstance _unshaded;

    public override OverlaySpace Space => OverlaySpace.ScreenSpace;

    public PosterPasteMarkerOverlay()
    {
        IoCManager.InjectDependencies(this);
        _sprite = _entMan.System<SpriteSystem>();
        _transform = _entMan.System<TransformSystem>();
        _unshaded = _prototypes.Index(UnshadedShader).Instance();
    }

    protected override void Draw(in OverlayDrawArgs args)
    {
        var viewer = _players.LocalSession?.AttachedEntity;
        if (viewer == null || !_entMan.HasComponent<PosterPasteContractWorkerComponent>(viewer.Value))
            return;

        if (args.ViewportControl == null)
            return;

        var matrix = args.ViewportControl.GetWorldToScreenMatrix();
        var handle = args.ScreenHandle;
        handle.UseShader(_unshaded);

        var xformQuery = _entMan.GetEntityQuery<TransformComponent>();
        var markerQuery = _entMan.AllEntityQueryEnumerator<PosterPasteMarkerComponent, TransformComponent>();
        var curTime = _timing.RealTime;
        var texture = _sprite.GetFrame(PaperRsi, curTime);
        var half = new Vector2(texture.Width / 2f, texture.Height / 2f);

        while (markerQuery.MoveNext(out var uid, out var marker, out var xform))
        {
            if (!marker.Active)
                continue;

            if (xform.MapID != args.MapId)
                continue;

            var world = _transform.GetWorldPosition(xform, xformQuery);
            var screen = Vector2.Transform(world, matrix);
            handle.DrawTexture(texture, screen - half);
        }

        handle.UseShader(null);
    }
}

