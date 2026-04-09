using Content.Shared.HL2RP.Denunciations.UI;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.HL2RP.Denunciations.UI;

[UsedImplicitly]
public sealed class DenunciationsTerminalBoundUserInterface : BoundUserInterface
{
    private DenunciationsTerminalWindow? _window;

    public DenunciationsTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<DenunciationsTerminalWindow>();
        _window.OnSelectCitizen += uid => SendMessage(new DenunciationsSelectCitizenMessage(uid));
        _window.OnSubmitDenunciation += (targetUid, reason, severity) =>
            SendMessage(new DenunciationsSubmitMessage(targetUid, reason, severity));
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
            return;

        if (state is DenunciationsTerminalBoundUiState s)
            _window.UpdateState(s);
    }
}
