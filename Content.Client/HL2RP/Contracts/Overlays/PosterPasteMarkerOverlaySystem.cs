using Robust.Client.Graphics;

namespace Content.Client.HL2RP.Contracts.Overlays;

public sealed class PosterPasteMarkerOverlaySystem : EntitySystem
{
    [Dependency] private readonly IOverlayManager _overlays = default!;

    public override void Initialize()
    {
        base.Initialize();
        _overlays.AddOverlay(new PosterPasteMarkerOverlay());
    }

    public override void Shutdown()
    {
        base.Shutdown();
        _overlays.RemoveOverlay<PosterPasteMarkerOverlay>();
    }
}

