// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared._CorvaxGoob.Traitor.HighRiskPinpointer;
using JetBrains.Annotations;
using Robust.Client.UserInterface;

namespace Content.Client._CorvaxGoob.Traitor.HighRiskPinpointer;

/// <summary>
/// Opens the target selection window, forwards search requests to the server, and preserves the current selection
/// while this bound interface remains active.
/// </summary>
[UsedImplicitly]
public sealed class HighRiskPinpointerUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    // Reuse the selected entry if the window is recreated without replacing the bound interface.
    private int _selectedTargetId;

    protected override void Open()
    {
        base.Open();

        var window = this.CreateWindow<HighRiskPinpointerWindow>();
        window.SearchSubmitted += (selectionId, dna) => SendMessage(new HighRiskPinpointerSearchMessage(selectionId, dna));
        window.SelectionChanged += targetId => _selectedTargetId = targetId;
        window.SelectTarget(_selectedTargetId);
    }
}
