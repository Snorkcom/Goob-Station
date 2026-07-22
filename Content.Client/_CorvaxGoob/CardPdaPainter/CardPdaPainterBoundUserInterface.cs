// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._CorvaxGoob.CardPdaPainter;
using Content.Shared.Containers.ItemSlots;
using Content.Shared.Roles;
using JetBrains.Annotations;
using Robust.Client.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Client._CorvaxGoob.CardPdaPainter;

[UsedImplicitly]
public sealed class CardPdaPainterBoundUserInterface : BoundUserInterface
{
    [Dependency] private readonly IPrototypeManager _prototype = default!;

    private CardPdaPainterWindow? _window;

    public CardPdaPainterBoundUserInterface(EntityUid owner, Enum uiKey) : base(owner, uiKey)
    {
        IoCManager.InjectDependencies(this);
    }

    protected override void Open()
    {
        base.Open();

        _window = this.CreateWindow<CardPdaPainterWindow>();
        _window.OnTargetSlotPressed += OnTargetSlotPressed;
        _window.OnPaintPressed += OnPaintPressed;

        if (State is CardPdaPainterBoundUserInterfaceState state)
            _window.UpdateState(state, _prototype);
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing && _window != null)
        {
            _window.OnTargetSlotPressed -= OnTargetSlotPressed;
            _window.OnPaintPressed -= OnPaintPressed;
        }

        base.Dispose(disposing);
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is CardPdaPainterBoundUserInterfaceState cast)
            _window?.UpdateState(cast, _prototype);
    }

    private void OnTargetSlotPressed()
    {
        SendPredictedMessage(new ItemSlotButtonPressedEvent(CardPdaPainterComponent.TargetSlotId));
    }

    private void OnPaintPressed(ProtoId<JobPrototype> jobId)
    {
        SendMessage(new CardPdaPainterRepaintMessage(jobId));
    }
}
