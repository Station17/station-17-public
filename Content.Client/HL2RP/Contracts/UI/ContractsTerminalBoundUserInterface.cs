using Content.Shared.HL2RP.Contracts.UI;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client.HL2RP.Contracts.UI;

[UsedImplicitly]
public sealed class ContractsTerminalBoundUserInterface : BoundUserInterface
{
    private ContractsTerminalWindow? _window;

    public ContractsTerminalBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<ContractsTerminalWindow>();
        _window.OnAccept += id => SendMessage(new ContractsAcceptMessage(id));
        _window.OnCancel += () => SendMessage(new ContractsCancelMessage());
        _window.OnClose += Close;
        _window.OpenCentered();
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (_window == null)
            return;

        if (state is ContractsTerminalBoundUiState s)
            _window.UpdateState(s);
    }
}

