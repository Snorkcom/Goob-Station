// SPDX-FileCopyrightText: 2026 Snorkcom <Snorkcom@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.VendingMachines;

public sealed partial class VendingMachineInventoryEntry
{
    /// <summary>
    /// UI-only marker showing that this prototype has at least one returned entity stored in the machine.
    /// </summary>
    /// <remarks>
    /// The client only needs this boolean for display; actual returned entities stay server-side in the hidden container.
    /// </remarks>
    [DataField]
    public bool HasReturnedItem;
}
