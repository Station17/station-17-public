using Content.Shared.HL2RP.CID.UI;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

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
        _window.OnBackToRecords += () => SendMessage(new CIDClearSelectedRecordMessage());
        _window.OnChangeRecordLp += (uid, lp) => SendMessage(new CIDUpdateSelectedLPMessage(uid, lp));
        _window.OnSelectDenunciation += id => SendMessage(new CIDSelectDenunciationMessage(id));
        _window.OnBackToDenunciations += () => SendMessage(new CIDClearSelectedDenunciationMessage());
        _window.OnTakeDenunciation += id => SendMessage(new CIDTakeDenunciationMessage(id));
        _window.OnCancelDenunciationResolution += id => SendMessage(new CIDCancelDenunciationResolutionMessage(id));
        _window.OnAcceptDenunciation += id => SendMessage(new CIDAcceptDenunciationMessage(id));
        _window.OnRejectDenunciation += id => SendMessage(new CIDRejectDenunciationMessage(id));
        _window.OnChangeCitizenJob += (cardUid, jobProtoId) =>
            SendMessage(new CIDChangeCitizenJobMessage(cardUid, new ProtoId<JobPrototype>(jobProtoId)));
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
