using Content.Shared.HL2RP.CID.UI;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.HL2RP.CID.UI;

[UsedImplicitly]
public sealed class CIDTabletBoundUserInterface : BoundUserInterface
{
    private CIDTabletWindow? _window;

    public CIDTabletBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CIDTabletWindow>();
        _window.OnGenerateNumber += () => SendMessage(new CIDGenerateNumberMessage());
        _window.OnWriteCard += (name, surname, cNumber) => SendMessage(new CIDWriteCardMessage(name, surname, cNumber));
        _window.OnSelectRecord += uid => SendMessage(new CIDSelectRecordMessage(uid));
        _window.OnChangeRecordLp += (uid, lp) => SendMessage(new CIDUpdateSelectedLPMessage(uid, lp));
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
            return;

        if (state is CIDTabletBoundUiState s)
            _window.UpdateState(s);
    }
}
