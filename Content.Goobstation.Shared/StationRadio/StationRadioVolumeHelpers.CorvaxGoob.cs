using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Utility;

namespace Content.Goobstation.Shared.StationRadio;

public static class StationRadioVolumeHelpers
{
    private static readonly SpriteSpecifier VolumeVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png"));

    public static void AddVolumeVerb<TComponent>(
        Entity<TComponent> ent,
        ref GetVerbsEvent<AlternativeVerb> args,
        SharedPowerReceiverSystem power,
        Action<Entity<TComponent>, EntityUid> openVolumeUi)
        where TComponent : IComponent
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!power.IsPowered(ent.Owner))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("station-radio-volume-verb"),
            Icon = VolumeVerbIcon,
            // Keep item-slot eject and other default alternative verbs above volume in alt-click priority.
            Priority = -1,
            Act = () => openVolumeUi(ent, user),
        });
    }

    public static bool CloseVolumeUiIfUnpowered(
        EntityUid uid,
        SharedPowerReceiverSystem power,
        SharedUserInterfaceSystem ui)
    {
        if (power.IsPowered(uid))
            return false;

        ui.CloseUi(uid, StationRadioVolumeUiKey.Key);
        return true;
    }

    public static bool OpenVolumeUi(
        EntityUid uid,
        EntityUid user,
        float volume,
        SharedPowerReceiverSystem power,
        SharedUserInterfaceSystem ui)
    {
        if (CloseVolumeUiIfUnpowered(uid, power, ui))
            return false;

        UpdateVolumeUi(uid, volume, ui);
        ui.TryOpenUi(uid, StationRadioVolumeUiKey.Key, user);
        return true;
    }

    public static void UpdateVolumeUi(EntityUid uid, float volume, SharedUserInterfaceSystem ui)
    {
        ui.SetUiState(uid, StationRadioVolumeUiKey.Key, new StationRadioVolumeState(volume));
    }
}
