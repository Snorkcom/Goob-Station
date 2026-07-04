using System.Collections.Generic;
using Content.Goobstation.Shared.StationRadio;
using Content.Goobstation.Shared.StationRadio.Components;
using Content.Server.Radio.Components;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Robust.Shared.Player;

namespace Content.Goobstation.Server.StationRadio;

public sealed partial class CassetteRadioSystem
{
    private void OnEquipped(Entity<CassetteRadioComponent> ent, ref GotEquippedEvent args)
    {
        if (!IsRadioCarrySlot(args.SlotFlags))
            return;

        SetRadioCarrier(ent, args.Equipee);
    }

    private void OnUnequipped(Entity<CassetteRadioComponent> ent, ref GotUnequippedEvent args)
    {
        if (!IsRadioCarrySlot(args.SlotFlags))
            return;

        ClearRadioCarrier(ent, args.Equipee);
    }

    private void OnEquippedHand(Entity<CassetteRadioComponent> ent, ref GotEquippedHandEvent args)
    {
        SetRadioCarrier(ent, args.User);
    }

    private void OnUnequippedHand(Entity<CassetteRadioComponent> ent, ref GotUnequippedHandEvent args)
    {
        ClearRadioCarrier(ent, args.User);
    }

    private void RefreshRadioReceiver(Entity<CassetteRadioComponent> ent)
    {
        if (!ent.Comp.Active || !TryGetWearerActor(ent, out _, out _))
        {
            RemCompDeferred<ActiveRadioComponent>(ent);
            return;
        }

        EnsureComp<ActiveRadioComponent>(ent).Channels = new HashSet<string> { ent.Comp.Channel };
    }

    /// <summary>
    /// Assigns a player as the personal radio listener when the cassette is in a supported carried slot.
    /// </summary>
    private void SetRadioCarrier(Entity<CassetteRadioComponent> ent, EntityUid carrier)
    {
        ent.Comp.Wearer = carrier;
        RefreshRadioReceiver(ent);
        TryStartCurrentMedia(ent);
    }

    /// <summary>
    /// Clears the listener only after the cassette is no longer held, pocketed, or worn on the neck.
    /// </summary>
    private void ClearRadioCarrier(Entity<CassetteRadioComponent> ent, EntityUid carrier)
    {
        if (ent.Comp.Wearer != carrier)
            return;

        if (IsCarriedBy(carrier, ent.Owner))
        {
            RefreshRadioReceiver(ent);
            TryStartCurrentMedia(ent);
            return;
        }

        ent.Comp.Wearer = null;
        StopMedia(ent);
        _ui.CloseUi(ent.Owner, StationRadioVolumeUiKey.Key);
        RefreshRadioReceiver(ent);
    }

    /// <summary>
    /// Finds every cassette player that can currently play private radio for the player.
    /// </summary>
    private IEnumerable<Entity<CassetteRadioComponent>> GetCarriedCassettes(EntityUid wearer)
    {
        foreach (var item in _inventory.GetHandOrInventoryEntities(wearer, RadioCarrySlotFlags))
        {
            if (TryComp<CassetteRadioComponent>(item, out var component))
                yield return (item, component);
        }
    }

    /// <summary>
    /// Checks hands plus the supported inventory slots so missed move order cannot leave stale playback.
    /// </summary>
    private bool IsCarriedBy(EntityUid wearer, EntityUid cassette)
    {
        foreach (var item in _inventory.GetHandOrInventoryEntities(wearer, RadioCarrySlotFlags))
        {
            if (item == cassette)
                return true;
        }

        return false;
    }

    private static bool IsRadioCarrySlot(SlotFlags flags)
    {
        return (flags & RadioCarrySlotFlags) != 0;
    }

    /// <summary>
    /// Requires both an equipped wearer and an attached player before creating personal audio or radio text.
    /// </summary>
    private bool TryGetWearerActor(Entity<CassetteRadioComponent> ent, out EntityUid wearer, out ActorComponent actor)
    {
        if (TryGetWearer(ent, out wearer) && TryComp(wearer, out actor!))
            return true;

        actor = default!;
        return false;
    }

    private bool TryGetWearer(Entity<CassetteRadioComponent> ent, out EntityUid wearer)
    {
        // The cached wearer is only valid while the item is still in an allowed carried location.
        if (ent.Comp.Wearer is { } currentWearer
            && Exists(currentWearer)
            && IsCarriedBy(currentWearer, ent.Owner))
        {
            wearer = currentWearer;
            return true;
        }

        wearer = default;
        return false;
    }
}
