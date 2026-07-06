using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Content.Goobstation.Shared.StationRadio.Systems;
using Content.Goobstation.Shared.StationRadio;
using Content.Server._EinsteinEngines.Language;
using Content.Server.Radio;
using Content.Shared.Chat;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Utility;

namespace Content.Goobstation.Server.StationRadio;

public sealed partial class CassetteRadioSystem : EntitySystem
{
    private static readonly SpriteSpecifier EnableRadioVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/AdminActions/play.png"));

    private static readonly SpriteSpecifier DisableRadioVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/AdminActions/pause.png"));

    private static readonly SpriteSpecifier VolumeVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png"));

    /// <summary>
    /// Personal radio is audible only while the cassette player is immediately carried by the listener.
    /// Hands are included by InventorySystem.GetHandOrInventoryEntities.
    /// </summary>
    private const SlotFlags RadioCarrySlotFlags = SlotFlags.NECK | SlotFlags.POCKET;

    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPopupSystem _popup = default!;
    [Dependency] private readonly LanguageSystem _language = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;
    [Dependency] private readonly InventorySystem _inventory = default!;
    [Dependency] private readonly VinylPlayerSystem _vinylPlayer = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CassetteRadioComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<CassetteRadioComponent, GotEquippedEvent>(OnEquipped);
        SubscribeLocalEvent<CassetteRadioComponent, GotUnequippedEvent>(OnUnequipped);
        SubscribeLocalEvent<CassetteRadioComponent, GotEquippedHandEvent>(OnEquippedHand);
        SubscribeLocalEvent<CassetteRadioComponent, GotUnequippedHandEvent>(OnUnequippedHand);
        SubscribeLocalEvent<CassetteRadioComponent, ComponentShutdown>(OnShutdown);
        SubscribeLocalEvent<CassetteRadioComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<CassetteRadioComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<CassetteRadioComponent, RadioReceiveEvent>(OnRadioReceive);
        SubscribeLocalEvent<CassetteRadioComponent, StationRadioSetVolumeMessage>(OnSetVolume);
        SubscribeLocalEvent<PlayerAttachedEvent>(OnPlayerAttached);
        SubscribeLocalEvent<PlayerDetachedEvent>(OnPlayerDetached);
    }

    private void OnGetVerbs(Entity<CassetteRadioComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString(ent.Comp.Active
                ? "cassette-radio-verb-disable"
                : "cassette-radio-verb-enable"),
            Icon = ent.Comp.Active
                ? DisableRadioVerbIcon
                : EnableRadioVerbIcon,
            Act = () => SetEnabled(ent, !ent.Comp.Active, user),
        });

        if (IsCarriedBy(user, ent.Owner))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("station-radio-volume-verb"),
                Icon = VolumeVerbIcon,
                // Keep primary item verbs above volume in alt-click priority.
                Priority = -1,
                Act = () => OpenVolumeUi(ent, user),
            });
        }
    }

    private void OnRadioReceive(Entity<CassetteRadioComponent> ent, ref RadioReceiveEvent args)
    {
        if (!ent.Comp.Active || args.Channel.ID != ent.Comp.Channel)
            return;

        if (!TryGetWearerActor(ent, out var wearer, out var actor))
            return;

        var message = _language.CanUnderstand(wearer, args.Language.ID)
            ? args.OriginalChatMsg
            : args.LanguageObfuscatedChatMsg;

        _net.ServerSendMessage(new MsgChatMessage { Message = message }, actor.PlayerSession.Channel);
    }

    private void OnSetVolume(Entity<CassetteRadioComponent> ent, ref StationRadioSetVolumeMessage args)
    {
        if (args.Actor is not { Valid: true } actor)
            return;

        if (!IsCarriedBy(actor, ent.Owner))
        {
            _ui.CloseUi(ent.Owner, StationRadioVolumeUiKey.Key);
            return;
        }

        ent.Comp.Volume = MathHelper.Clamp(args.Volume, 0f, 1f);

        if (ent.Comp.Active && TryGetWearerActor(ent, out _, out _))
            ApplyMediaVolume(ent);

        UpdateVolumeUi(ent);
    }

    private void OpenVolumeUi(Entity<CassetteRadioComponent> ent, EntityUid user)
    {
        if (!IsCarriedBy(user, ent.Owner))
        {
            _ui.CloseUi(ent.Owner, StationRadioVolumeUiKey.Key);
            return;
        }

        UpdateVolumeUi(ent);
        _ui.TryOpenUi(ent.Owner, StationRadioVolumeUiKey.Key, user);
    }

    private void UpdateVolumeUi(Entity<CassetteRadioComponent> ent)
    {
        _ui.SetUiState(ent.Owner, StationRadioVolumeUiKey.Key, new StationRadioVolumeState(ent.Comp.Volume));
    }

    private void SetEnabled(Entity<CassetteRadioComponent> ent, bool enabled, EntityUid user)
    {
        if (ent.Comp.Active == enabled)
            return;

        ent.Comp.Active = enabled;

        if (!enabled)
            StopMedia(ent);

        RefreshRadioReceiver(ent);

        if (enabled)
            TryStartCurrentMedia(ent);

        var popup = enabled
            ? TryGetWearer(ent, out _)
                ? "cassette-radio-popup-enabled"
                : "cassette-radio-popup-enabled-no-neck"
            : "cassette-radio-popup-disabled";

        _popup.PopupEntity(Loc.GetString(popup), ent, user);
    }
}
