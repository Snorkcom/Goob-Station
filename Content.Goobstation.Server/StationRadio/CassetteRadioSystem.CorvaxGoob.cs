using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Content.Goobstation.Shared.StationRadio.Systems;
using Content.Goobstation.Shared.StationRadio;
using Content.Server._EinsteinEngines.Language;
using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Shared.Chat;
using Content.Shared.Hands;
using Content.Shared.Inventory;
using Content.Shared.Inventory.Events;
using Content.Shared.Popups;
using Content.Shared.Radio;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Player;
using Robust.Shared.Timing;
using Robust.Shared.Utility;

namespace Content.Goobstation.Server.StationRadio;

public sealed class CassetteRadioSystem : EntitySystem
{
    private static readonly SpriteSpecifier EnableRadioVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/AdminActions/play.png"));

    private static readonly SpriteSpecifier DisableRadioVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/AdminActions/pause.png"));

    private static readonly SpriteSpecifier VolumeVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png"));

    private static readonly SpriteSpecifier ResyncRadioVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/refresh.svg.192dpi.png"));

    private static readonly TimeSpan ResyncCooldown = TimeSpan.FromSeconds(5);

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
    [Dependency] private readonly IGameTiming _timing = default!;

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

        if (CanResyncRadio(ent, user))
        {
            args.Verbs.Add(new AlternativeVerb
            {
                Text = Loc.GetString("cassette-radio-verb-resync"),
                Icon = ResyncRadioVerbIcon,
                Priority = -2,
                Act = () => ResyncRadio(ent, user),
            });
        }

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("station-radio-volume-verb"),
            Icon = VolumeVerbIcon,
            // Keep primary item verbs above volume in alt-click priority.
            Priority = -1,
            Act = () => OpenVolumeUi(ent, user),
        });
    }

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

    private void OnShutdown(Entity<CassetteRadioComponent> ent, ref ComponentShutdown args)
    {
        StopMedia(ent);
    }

    private void OnMediaPlayed(Entity<CassetteRadioComponent> ent, ref StationRadioMediaPlayedEvent args)
    {
        StopMedia(ent);
        // Personal streams query the clock at creation so reconnects and slot moves use the freshest offset.
        TryStartCurrentMedia(ent);
    }

    private void OnMediaStopped(Entity<CassetteRadioComponent> ent, ref StationRadioMediaStoppedEvent args)
    {
        StopMedia(ent);
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
        if (args.Actor is not { Valid: true })
            return;

        ent.Comp.Volume = MathHelper.Clamp(args.Volume, 0f, 1f);

        if (ent.Comp.Active && TryGetWearerActor(ent, out _, out _))
            SetMediaGain(ent, true);

        UpdateVolumeUi(ent);
    }

    private void OpenVolumeUi(Entity<CassetteRadioComponent> ent, EntityUid user)
    {
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

    private void TryStartCurrentMedia(Entity<CassetteRadioComponent> ent)
    {
        if (!ent.Comp.Active || !TryGetWearerActor(ent, out _, out var actor))
            return;

        if (ent.Comp.SoundEntity != null)
        {
            if (ent.Comp.SoundSession != actor.PlayerSession)
            {
                StopMedia(ent);
            }
            else
            {
                SetMediaGain(ent, true);
                return;
            }
        }

        if (!_vinylPlayer.TryGetCurrentRadioMedia(out var media))
            return;

        StartMedia(ent, media.Media, _vinylPlayer.GetCurrentRadioMediaOffset(media), media.StartTime);
    }

    private bool CanResyncRadio(Entity<CassetteRadioComponent> ent, EntityUid user)
    {
        return ent.Comp.Active
            && IsCarriedBy(user, ent.Owner)
            && _timing.CurTime >= ent.Comp.NextResyncTime
            && _vinylPlayer.TryGetCurrentRadioMedia(out _);
    }

    private void ResyncRadio(Entity<CassetteRadioComponent> ent, EntityUid user)
    {
        if (!ent.Comp.Active || !IsCarriedBy(user, ent.Owner) || !TryComp(user, out ActorComponent? actor))
            return;

        if (_timing.CurTime < ent.Comp.NextResyncTime)
            return;

        if (!_vinylPlayer.TryGetCurrentRadioMedia(out var media))
            return;

        ent.Comp.NextResyncTime = _timing.CurTime + ResyncCooldown;
        ent.Comp.Wearer = user;
        StopMedia(ent);
        StartMedia(ent, media.Media, _vinylPlayer.GetCurrentRadioMediaOffset(media), media.StartTime);
        RefreshRadioReceiver(ent);

        RaiseNetworkEvent(new RadioPlaybackResyncEvent(), actor.PlayerSession);
        _popup.PopupEntity(Loc.GetString("cassette-radio-popup-resynced"), ent, user);
    }

    /// <summary>
    /// Recreates personal global audio after the client gets a fresh player session.
    /// </summary>
    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        foreach (var cassette in GetCarriedCassettes(args.Entity))
        {
            cassette.Comp.Wearer = args.Entity;
            RefreshRadioReceiver(cassette);
            TryStartCurrentMedia(cassette);
        }
    }

    /// <summary>
    /// Clears session-bound audio before the old player session disappears.
    /// </summary>
    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        foreach (var cassette in GetCarriedCassettes(args.Entity))
        {
            if (cassette.Comp.Wearer != args.Entity)
                continue;

            StopMedia(cassette);
            RefreshRadioReceiver(cassette);
        }
    }

    private void StartMedia(Entity<CassetteRadioComponent> ent, SoundPathSpecifier media, float offset, TimeSpan broadcastStartTime)
    {
        if (!ent.Comp.Active || ent.Comp.SoundEntity != null)
            return;

        if (!TryGetWearerActor(ent, out _, out var actor))
            return;

        var resolved = _audio.ResolveSound(media);
        if (_audio.GetAudioLength(resolved).TotalSeconds <= offset)
            return;

        var audio = _audio.PlayGlobal(resolved, actor.PlayerSession, ent.Comp.AudioParams
            .WithPlayOffset(offset)
            .WithVolume(GetRadioVolume(ent.Comp)));
        if (audio == null)
            return;

        ent.Comp.SoundEntity = audio.Value.Entity;
        ent.Comp.SoundSession = actor.PlayerSession;
        // Keep the server-side AudioStart aligned for clients that receive this stream after it was created.
        _audio.SetPlaybackPosition(new Entity<AudioComponent?>(audio.Value.Entity, audio.Value.Component), offset);

        var synced = EnsureComp<RadioSyncedAudioComponent>(audio.Value.Entity);
        synced.BroadcastStartTime = broadcastStartTime;
        Dirty(audio.Value.Entity, synced);
    }

    private void SetMediaGain(Entity<CassetteRadioComponent> ent, bool audible)
    {
        if (ent.Comp.SoundEntity == null)
            return;

        if (audible)
            _audio.SetVolume(ent.Comp.SoundEntity, GetRadioVolume(ent.Comp));
        else
            _audio.SetGain(ent.Comp.SoundEntity, 0f);
    }

    private static float GetRadioVolume(CassetteRadioComponent comp)
    {
        return comp.AudioParams.Volume + SharedAudioSystem.GainToVolume(comp.Volume);
    }

    private void StopMedia(Entity<CassetteRadioComponent> ent)
    {
        if (ent.Comp.SoundEntity == null)
        {
            ent.Comp.SoundSession = null;
            return;
        }

        ent.Comp.SoundEntity = _audio.Stop(ent.Comp.SoundEntity);
        ent.Comp.SoundSession = null;
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
