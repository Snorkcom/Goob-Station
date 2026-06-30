// SPDX-FileCopyrightText: 2026 Corvax-Forge
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using static Content.Shared.Access.Components.IdCardConsoleComponent;

namespace Content.Client.Access.UI
{
    public sealed partial class IdCardConsoleWindow // corvax goob edit - made partial
    {
        private bool _pendingBulkAccessUpdate;

        /// <summary>
        /// Wires the bulk access buttons to their corresponding server-side ID console actions.
        /// </summary>
        /// <remarks>
        /// The client only sends the selected <see cref="IdCardConsoleBulkAccessAction"/>; all access validation and mutation happen on the server.
        /// </remarks>
        private void InitializeCorvaxGoobBulkAccessButtons()
        {
            DemoteToPassengerButton.OnPressed += _ => SubmitBulkAccessAction(IdCardConsoleBulkAccessAction.DemoteToPassenger);
            StandardAccessButton.OnPressed += _ => SubmitBulkAccessAction(IdCardConsoleBulkAccessAction.StandardAccess);
            ExtendedAccessButton.OnPressed += _ => SubmitBulkAccessAction(IdCardConsoleBulkAccessAction.Extended);
            FullAccessButton.OnPressed += _ => SubmitBulkAccessAction(IdCardConsoleBulkAccessAction.Full);
        }

        // Tracks a pending bulk-access response so the next UpdateState overwrites any unsaved
        // manual JobTitleLineEdit text with the authoritative job title from the target ID card.
        private void SubmitBulkAccessAction(IdCardConsoleBulkAccessAction action)
        {
            _pendingBulkAccessUpdate = true;
            _owner.SubmitBulkAccessAction(action);
        }

        private void SyncJobTitleAfterBulkAccess(string targetJobTitle)
        {
            if (!_pendingBulkAccessUpdate)
                return;

            // Bulk actions mutate the card server-side, so the next state must replace any unsaved manual title edit with the authoritative card title.
            JobTitleLineEdit.Text = targetJobTitle;
            _pendingBulkAccessUpdate = false;
        }

        private void SetCorvaxGoobBulkButtonsDisabled(bool disabled)
        {
            DemoteToPassengerButton.Disabled = disabled;
            StandardAccessButton.Disabled = disabled;
            ExtendedAccessButton.Disabled = disabled;
            FullAccessButton.Disabled = disabled;
        }
    }
}
