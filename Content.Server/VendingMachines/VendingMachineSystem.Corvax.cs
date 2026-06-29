// SPDX-FileCopyrightText: 2026 Snorkcom <Snorkcom@users.noreply.github.com>
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using Content.Server.Power.EntitySystems;
using Content.Shared.Destructible;
using Content.Shared.Examine;
using Content.Shared.Hands.EntitySystems;
using Content.Shared.Interaction;
using Content.Shared.VendingMachines;
using Robust.Shared.Containers;
using Robust.Shared.Map;
using Robust.Shared.Utility;

namespace Content.Server.VendingMachines
{
    public sealed partial class VendingMachineSystem
    {
        [Dependency] private readonly SharedContainerSystem _container = default!;
        [Dependency] private readonly SharedHandsSystem _hands = default!;

        private void InitializeVendingReturn()
        {
            SubscribeLocalEvent<VendingMachineComponent, ComponentInit>(OnComponentInit);

            // Some closed Openable items handle fallback interactions before the vending machine can see them.
            // Use InteractUsingEvent so those items can still be returned before OpenableSystem blocks the click.
            SubscribeLocalEvent<VendingMachineComponent, InteractUsingEvent>(OnInteractUsing);

            SubscribeLocalEvent<VendingMachineComponent, ExaminedEvent>(OnExamined);
            SubscribeLocalEvent<VendingMachineComponent, DestructionEventArgs>(OnDestruction);
        }

        private void OnComponentInit(Entity<VendingMachineComponent> ent, ref ComponentInit args)
        {
            // Returned items are real entities stored in the machine, while vending inventory only tracks prototype counts.
            ent.Comp.ReturnedInventoryContainer = _container.EnsureContainer<Container>(ent, VendingMachineComponent.ReturnedInventoryContainerId);
        }

        private void OnInteractUsing(EntityUid uid, VendingMachineComponent component, InteractUsingEvent args)
        {
            if (args.Handled)
                return;

            // Only consume the click when the item was actually accepted, preserving normal interactions otherwise.
            args.Handled = TryReturnItem((uid, component), args.User, args.Used);
        }

        private bool TryReturnItem(Entity<VendingMachineComponent> vending, EntityUid user, EntityUid used)
        {
            var (uid, component) = vending;

            // Restock boxes have their own interaction path and must not be handled by the new return flow.
            if (HasComp<VendingMachineRestockComponent>(used))
                return false;

            // Only accept item types that already exist in this machine's configured inventory.
            var prototype = MetaData(used).EntityPrototype?.ID;
            if (prototype == null || !TryGetReturnableEntry(component, prototype, out var entry))
                return false;

            // Do not accept returned items if the machine is broken or has no power.
            if (component.Broken || !this.IsPowered(uid, EntityManager))
                return false;

            // Keep the stored entity and displayed stock count in sync by moving the item before incrementing stock.
            if (!_hands.TryDropIntoContainer(user, used, component.ReturnedInventoryContainer))
                return false;

            // Store the returned entity by prototype so the machine can vend that exact item instead of a new entity.
            component.ReturnedInventory ??= new();
            component.ReturnedInventory.GetOrNew(prototype).Add(used);
            entry.Amount++;
            Dirty(uid, component);
            UpdateUI((uid, component));

            Popup.PopupEntity(Loc.GetString("vending-machine-component-return-success",
                ("item", used),
                ("target", uid)), user, user);
            return true;
        }

        private static bool TryGetReturnableEntry(
            VendingMachineComponent component,
            string prototype,
            [NotNullWhen(true)] out VendingMachineInventoryEntry? entry)
        {
            // Returned items can refill regular, emagged, or contraband inventory entries.
            if (component.Inventory.TryGetValue(prototype, out entry) ||
                component.EmaggedInventory.TryGetValue(prototype, out entry) ||
                component.ContrabandInventory.TryGetValue(prototype, out entry))
                return true;

            entry = null;
            return false;
        }

        private void OnExamined(EntityUid uid, VendingMachineComponent component, ExaminedEvent args)
        {
            if (!HasStoredReturnedItems(component))
                return;

            var message = Loc.GetString("vending-machine-component-returned-items-examine");
            args.PushMarkup($"[color=yellow]{message}[/color]");
        }

        private bool HasStoredReturnedItems(VendingMachineComponent component)
        {
            if (component.ReturnedInventory is null)
                return false;

            foreach (var returned in component.ReturnedInventory.Values)
            {
                foreach (var item in returned)
                {
                    // Only show the examine hint for items still physically stored inside this machine.
                    if (!Deleted(item) && component.ReturnedInventoryContainer.Contains(item))
                        return true;
                }
            }

            return false;
        }

        private void OnDestruction(EntityUid uid, VendingMachineComponent component, DestructionEventArgs args)
        {
            if (component.ReturnedInventory is null)
                return;

            var coordinates = Transform(uid).Coordinates;

            // When the vending machine is destroyed, all previously returned items stored inside it fall out.
            foreach (var returned in component.ReturnedInventory.Values)
            {
                foreach (var item in returned)
                {
                    if (Deleted(item))
                        continue;

                    _container.Remove(item, component.ReturnedInventoryContainer, destination: coordinates);
                }
            }

            component.ReturnedInventory.Clear();
        }

        private void UpdateReturnedItemMarkerAfterVend(EntityUid uid, VendingMachineComponent component, bool usedReturnedItem)
        {
            if (!usedReturnedItem)
                return;

            // The UI marker is computed from component state, so refresh it after removing a returned entity.
            Dirty(uid, component);
            UpdateUI((uid, component));
        }

        private bool TryTakeReturnedItemForVend(VendingMachineComponent component, string itemId, EntityCoordinates spawnCoordinates, out EntityUid item)
        {
            item = default;

            // No returned entity is stored for this prototype, so the caller should spawn normally.
            if (component.ReturnedInventory is null ||
                !component.ReturnedInventory.TryGetValue(itemId, out var returned))
                return false;

            // Remove from the end so List<T> does not need to shift the remaining entries.
            while (returned.Count > 0)
            {
                var index = returned.Count - 1;
                item = returned[index];
                returned.RemoveAt(index);

                // Skip stale entries if the stored entity was deleted or cannot be removed from storage.
                if (Deleted(item) || !_container.Remove(item, component.ReturnedInventoryContainer, destination: spawnCoordinates))
                    continue;

                // No returned items of this type are left, so remove the entry from the lookup dictionary.
                if (returned.Count == 0)
                    component.ReturnedInventory.Remove(itemId);

                return true;
            }

            // None of the saved returned items could be used, so remove this type from the lookup dictionary.
            component.ReturnedInventory.Remove(itemId);
            return false;
        }
    }
}
