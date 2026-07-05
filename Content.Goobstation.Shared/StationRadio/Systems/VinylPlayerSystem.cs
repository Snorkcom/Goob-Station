using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Content.Goobstation.Shared.StationRadio;
using Content.Shared.Destructible;
using Content.Shared.DeviceLinking;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Network;

namespace Content.Goobstation.Shared.StationRadio.Systems;

public sealed partial class VinylPlayerSystem : EntitySystem // CorvaxGoob Edit - made partial
{
    [Dependency] private readonly INetManager _net = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedDeviceLinkSystem _deviceLinkSystem = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<VinylPlayerComponent, EntInsertedIntoContainerMessage>(OnVinylInserted);
        SubscribeLocalEvent<VinylPlayerComponent, EntRemovedFromContainerMessage>(OnVinylRemove);
        SubscribeLocalEvent<VinylPlayerComponent, DestructionEventArgs>(OnDestruction);
        SubscribeLocalEvent<VinylPlayerComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<VinylPlayerComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<VinylPlayerComponent, StationRadioSetVolumeMessage>(OnSetVolume);
    }

    private void OnPowerChanged(EntityUid uid, VinylPlayerComponent comp, PowerChangedEvent args)
    {
        if (!args.Powered)
            StationRadioVolumeHelpers.CloseVolumeUiIfUnpowered(uid, _power, _ui);

        if (comp.SoundEntity != null && !args.Powered)
        {
            comp.SoundEntity = _audio.Stop(comp.SoundEntity);
            Dirty(uid, comp);
        }

        if (!CheckForRadioRig(uid))
            return;

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out _))
        {
            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }

        StopCurrentRadioMedia(); // CorvaxGoob - CassetteRadio
    }

    private void OnDestruction(EntityUid uid, VinylPlayerComponent comp, DestructionEventArgs args)
    {
        if (!CheckForRadioRig(uid))
            return;

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var _))
        {
            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }

        StopCurrentRadioMedia(); // CorvaxGoob - CassetteRadio
    }

    private void OnVinylInserted(EntityUid uid, VinylPlayerComponent comp, EntInsertedIntoContainerMessage args)
    {
        if (!TryComp(args.Entity, out VinylComponent? vinylcomp) || _net.IsClient || vinylcomp.Song == null || !_power.IsPowered(uid))
            return;

        var hasRadioBroadcast = TryPrepareRadioBroadcast(uid, vinylcomp.Song, out var playOffset, out var startedRadioBroadcast);
        StartLocalVinylAudio(uid, comp, vinylcomp.Song, playOffset, hasRadioBroadcast);

        // Used by VinylSummonRuleSystem
        var ev = new VinylInsertedEvent(args.Entity);
        RaiseLocalEvent(uid, ref ev);

        if (!startedRadioBroadcast)
            return;

        RelayRadioMedia(vinylcomp.Song, playOffset);
    }

    /// <summary>
    /// Starts the shared radio clock when this player is wired into the radio rig.
    /// </summary>
    private bool TryPrepareRadioBroadcast(EntityUid uid, SoundPathSpecifier media, out float playOffset, out bool startedRadioBroadcast)
    {
        playOffset = 0f;
        startedRadioBroadcast = false;

        if (!CheckForRadioRig(uid))
            return false;

        startedRadioBroadcast = TryStartCurrentRadioMedia(media, out var radioMedia);
        playOffset = GetRadioMediaOffset(radioMedia);
        return true;
    }

    /// <summary>
    /// Plays the local vinyl source at the radio clock offset so late-created streams stay aligned.
    /// </summary>
    private void StartLocalVinylAudio(EntityUid uid, VinylPlayerComponent comp, SoundPathSpecifier media, float playOffset, bool markRadioSynced)
    {
        var audio = _audio.PlayPredicted(media, uid, uid, comp.AudioParams
            .WithVolume(GetVinylVolume(comp))
            .WithPlayOffset(playOffset));
        if (audio == null)
            return;

        comp.SoundEntity = audio.Value.Entity;
        // WithPlayOffset starts the client source; SetPlaybackPosition also fixes server AudioStart for late PVS.
        _audio.SetPlaybackPosition(new Entity<AudioComponent?>(audio.Value.Entity, audio.Value.Component), playOffset);

        if (markRadioSynced)
            EnsureComp<RadioSyncedAudioComponent>(audio.Value.Entity);

        Dirty(uid, comp);
    }

    /// <summary>
    /// Relays the current record to station receivers and personal cassette radio streams.
    /// </summary>
    private void RelayRadioMedia(SoundPathSpecifier media, float playOffset)
    {
        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var receiverComponent))
        {
            if (!receiverComponent.SoundEntity.HasValue)
                RaiseLocalEvent(receiver, new StationRadioMediaPlayedEvent(media, playOffset));
        }

        PlayCassetteRadioMedia(media, playOffset); // CorvaxGoob - CassetteRadio
    }

    private void OnVinylRemove(EntityUid uid, VinylPlayerComponent comp, EntRemovedFromContainerMessage args)
    {
        if (comp.SoundEntity != null)
        {
            comp.SoundEntity = _audio.Stop(comp.SoundEntity);
            Dirty(uid, comp);
        }

        // Used by VinylSummonRuleSystem
        var ev = new VinylRemovedEvent(args.Entity);
        RaiseLocalEvent(uid, ref ev);

        if (!CheckForRadioRig(uid))
            return;

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var _))
        {
            RaiseLocalEvent(receiver, new StationRadioMediaStoppedEvent());
        }

        StopCurrentRadioMedia(); // CorvaxGoob - CassetteRadio
    }

    private bool CheckForRadioRig(EntityUid uid)
    {
        if (TryComp<DeviceLinkSourceComponent>(uid, out var source))
        {
            foreach (var linked in source.LinkedPorts.Keys)
            {
                if (HasComp<RadioRigComponent>(linked) && CheckForRadioServer(linked))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private bool CheckForRadioServer(EntityUid uid)
    {
        if (TryComp<DeviceLinkSinkComponent>(uid, out var source))
        {
            foreach (var linked in source.LinkedSources)
            {
                if (HasComp<StationRadioServerComponent>(linked))
                {
                    return true;
                }
            }
        }
        return false;
    }

    private void OnGetVerbs(Entity<VinylPlayerComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        StationRadioVolumeHelpers.AddVolumeVerb(ent, ref args, _power, OpenVolumeUi);
    }

    private void OnSetVolume(Entity<VinylPlayerComponent> ent, ref StationRadioSetVolumeMessage args)
    {
        if (args.Actor is not { Valid: true })
            return;

        if (!_power.IsPowered(ent.Owner))
        {
            StationRadioVolumeHelpers.CloseVolumeUiIfUnpowered(ent.Owner, _power, _ui);
            return;
        }

        ent.Comp.Volume = MathHelper.Clamp(args.Volume, 0f, 1f);
        Dirty(ent.Owner, ent.Comp);

        if (ent.Comp.SoundEntity != null)
            _audio.SetVolume(ent.Comp.SoundEntity, GetVinylVolume(ent.Comp));

        StationRadioVolumeHelpers.UpdateVolumeUi(ent.Owner, ent.Comp.Volume, _ui);
    }

    private void OpenVolumeUi(Entity<VinylPlayerComponent> ent, EntityUid user)
    {
        StationRadioVolumeHelpers.OpenVolumeUi(ent.Owner, user, ent.Comp.Volume, _power, _ui);
    }

    /// <summary>
    /// Combines the record player's base local audio volume with the per-device slider multiplier.
    /// </summary>
    private static float GetVinylVolume(VinylPlayerComponent comp)
    {
        return comp.AudioParams.Volume + SharedAudioSystem.GainToVolume(comp.Volume);
    }
}
