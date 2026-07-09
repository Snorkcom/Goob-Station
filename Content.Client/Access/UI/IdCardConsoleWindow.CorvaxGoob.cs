// SPDX-FileCopyrightText: 2026 Snorkcom <Snorkcom@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using static Content.Shared.Access.Components.IdCardConsoleComponent;

namespace Content.Client.Access.UI
{
    public sealed partial class IdCardConsoleWindow
    {
        private bool _pendingBulkAccessUpdate;

        /// <summary>
        /// Connects the bulk access buttons (Standard, Extended, and Full) to their server actions.
        /// </summary>
        private void InitializeCorvaxGoobBulkAccessButtons()
        {
            StandardAccessButton.OnPressed += _ => SubmitBulkAccessAction(IdCardConsoleBulkAccessAction.StandardAccess);
            ExtendedAccessButton.OnPressed += _ => SubmitBulkAccessAction(IdCardConsoleBulkAccessAction.Extended);
            FullAccessButton.OnPressed += _ => SubmitBulkAccessAction(IdCardConsoleBulkAccessAction.Full);
        }

        /// <summary>
        /// Marks that a bulk action is pending and sends it to the server, so the next UpdateState can resync JobTitleLineEdit from the ID card.
        /// </summary>
        private void SubmitBulkAccessAction(IdCardConsoleBulkAccessAction action)
        {
            _pendingBulkAccessUpdate = true;
            _owner.SubmitBulkAccessAction(action);
        }

        /// <summary>
        /// After a bulk action, replaces any unsaved JobTitleLineEdit text with the job title currently stored on the target ID card.
        /// </summary>
        private void SyncJobTitleAfterBulkAccess(string targetJobTitle)
        {
            if (!_pendingBulkAccessUpdate)
                return;

            JobTitleLineEdit.Text = targetJobTitle;
            _pendingBulkAccessUpdate = false;
        }

        private void SetCorvaxGoobBulkButtonsDisabled(bool disabled)
        {
            StandardAccessButton.Disabled = disabled;
            ExtendedAccessButton.Disabled = disabled;
            FullAccessButton.Disabled = disabled;
        }
    }
}
