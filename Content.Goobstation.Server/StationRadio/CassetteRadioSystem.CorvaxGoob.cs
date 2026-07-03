using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Content.Goobstation.Shared.StationRadio.Systems;
using Content.Goobstation.Shared.StationRadio;
using Content.Server._EinsteinEngines.Language;
using Content.Server.Radio;
using Content.Server.Radio.Components;
using Content.Shared.Chat;
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

        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("station-radio-volume-verb"),
            Icon = VolumeVerbIcon,
            Act = () => OpenVolumeUi(ent, user),
        });
    }

    private void OnEquipped(Entity<CassetteRadioComponent> ent, ref GotEquippedEvent args)
    {
        if (!args.SlotFlags.HasFlag(SlotFlags.NECK))
            return;

        ent.Comp.Wearer = args.Equipee;
        RefreshRadioReceiver(ent);
        TryStartCurrentMedia(ent);
    }

    private void OnUnequipped(Entity<CassetteRadioComponent> ent, ref GotUnequippedEvent args)
    {
        if (!args.SlotFlags.HasFlag(SlotFlags.NECK) || ent.Comp.Wearer != args.Equipee)
            return;

        ent.Comp.Wearer = null;
        StopMedia(ent);
        RefreshRadioReceiver(ent);
    }

    private void OnShutdown(Entity<CassetteRadioComponent> ent, ref ComponentShutdown args)
    {
        StopMedia(ent);
    }

    private void OnMediaPlayed(Entity<CassetteRadioComponent> ent, ref StationRadioMediaPlayedEvent args)
    {
        StopMedia(ent);
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

        StartMedia(ent, media.Media, _vinylPlayer.GetCurrentRadioMediaOffset(media));
    }

    /// <summary>
    /// Recreates personal global audio after the client gets a fresh player session.
    /// </summary>
    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        if (!TryGetNeckCassette(args.Entity, out var cassette))
            return;

        cassette.Comp.Wearer = args.Entity;
        RefreshRadioReceiver(cassette);
        TryStartCurrentMedia(cassette);
    }

    /// <summary>
    /// Clears session-bound audio before the old player session disappears.
    /// </summary>
    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        if (!TryGetNeckCassette(args.Entity, out var cassette) || cassette.Comp.Wearer != args.Entity)
            return;

        StopMedia(cassette);
        RefreshRadioReceiver(cassette);
    }

    private void StartMedia(Entity<CassetteRadioComponent> ent, SoundPathSpecifier media, float offset)
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
        _audio.SetPlaybackPosition(new Entity<AudioComponent?>(audio.Value.Entity, audio.Value.Component), offset);
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
    /// Finds the cassette currently equipped in the player's neck slot.
    /// </summary>
    private bool TryGetNeckCassette(EntityUid wearer, out Entity<CassetteRadioComponent> cassette)
    {
        var slotEnumerator = _inventory.GetSlotEnumerator(wearer, SlotFlags.NECK);
        while (slotEnumerator.NextItem(out var item, out _))
        {
            if (!TryComp<CassetteRadioComponent>(item, out var component))
                continue;

            cassette = (item, component);
            return true;
        }

        cassette = default;
        return false;
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
        if (ent.Comp.Wearer is { } currentWearer && Exists(currentWearer))
        {
            wearer = currentWearer;
            return true;
        }

        wearer = default;
        return false;
    }
}
