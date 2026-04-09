using Content.Shared.HL2RP.Contracts.Components;
using Robust.Client.GameObjects;
using Robust.Client.Graphics;
using Robust.Client.Player;
using Robust.Shared.Prototypes;
using System.Linq;

namespace Content.Client.HL2RP.Contracts.Overlays;

public sealed class CargoBoxHighlightSystem : EntitySystem
{
    private static readonly ProtoId<ShaderPrototype> OutlineShaderProto = "SelectionOutlineInrange";

    [Dependency] private readonly IPlayerManager _players = default!;
    [Dependency] private readonly IPrototypeManager _prototypes = default!;

    private readonly Dictionary<EntityUid, ShaderInstance> _activeOutlines = new();

    public override void Shutdown()
    {
        base.Shutdown();

        foreach (var (uid, shader) in _activeOutlines)
        {
            if (Deleted(uid) || !TryComp<SpriteComponent>(uid, out var sprite))
                continue;

            if (ReferenceEquals(sprite.PostShader, shader))
                sprite.PostShader = null;
        }

        _activeOutlines.Clear();
    }

    public override void FrameUpdate(float frameTime)
    {
        base.FrameUpdate(frameTime);
        var viewer = _players.LocalSession?.AttachedEntity;
        var canSeeHighlights = viewer != null && HasComp<CargoBoxContractWorkerComponent>(viewer.Value);
        var outlinePrototype = _prototypes.Index(OutlineShaderProto);

        foreach (var entry in _activeOutlines.ToArray())
        {
            var uid = entry.Key;
            var shader = entry.Value;

            if (!TryComp<SpriteComponent>(uid, out var sprite))
            {
                _activeOutlines.Remove(uid);
                continue;
            }

            if (!ReferenceEquals(sprite.PostShader, shader))
                _activeOutlines.Remove(uid);
        }

        var query = EntityQueryEnumerator<CargoBoxContractItemComponent, SpriteComponent>();
        while (query.MoveNext(out var uid, out var box, out var sprite))
        {
            var shouldHighlight = canSeeHighlights && box.HighlightEnabled;

            if (shouldHighlight)
            {
                if (_activeOutlines.ContainsKey(uid))
                    continue;

                if (sprite.PostShader != null)
                    continue;

                var shader = outlinePrototype.InstanceUnique();
                shader.SetParameter("outline_width", 1f);
                sprite.PostShader = shader;
                _activeOutlines[uid] = shader;
            }
            else
            {
                if (!_activeOutlines.TryGetValue(uid, out var shader))
                    continue;

                if (ReferenceEquals(sprite.PostShader, shader))
                    sprite.PostShader = null;

                _activeOutlines.Remove(uid);
            }
        }
    }
}
