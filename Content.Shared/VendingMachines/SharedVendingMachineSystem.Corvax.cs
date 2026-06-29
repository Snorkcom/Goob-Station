// SPDX-FileCopyrightText: 2026 Snorkcom <Snorkcom@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

namespace Content.Shared.VendingMachines;

public abstract partial class SharedVendingMachineSystem
{
    private VendingMachineInventoryEntry CopyInventoryEntry(VendingMachineComponent component, VendingMachineInventoryEntry entry)
    {
        // Component state sends copied inventory entries to the client, so attach the UI marker here.
        var copy = new VendingMachineInventoryEntry(entry)
        {
            HasReturnedItem = HasStoredReturnedItem(component, entry.ID),
        };

        return copy;
    }

    private bool HasStoredReturnedItem(VendingMachineComponent component, string itemId)
    {
        if (component.ReturnedInventory is null ||
            !component.ReturnedInventory.TryGetValue(itemId, out var returned))
            return false;

        foreach (var item in returned)
        {
            // Ignore stale references; the marker should only show entities still stored in this machine.
            if (!Deleted(item) && component.ReturnedInventoryContainer.Contains(item))
                return true;
        }

        return false;
    }
}
