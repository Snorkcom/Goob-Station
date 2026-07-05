using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Content.Goobstation.Shared.Audio;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Robust.Shared.Audio.Systems;

namespace Content.Goobstation.Shared.StationRadio.Systems;

public sealed class StationRadioReceiverSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SingleStreamAudioVolumeSystem _volume = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<StationRadioReceiverComponent, ActivateInWorldEvent>(OnRadioToggle);
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);
    }

    private void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
        if (comp.SoundEntity == null)
            return;

        _volume.SetMuted(uid, !args.Powered || !comp.Active);
    }

    private void OnRadioToggle(EntityUid uid, StationRadioReceiverComponent comp, ActivateInWorldEvent args)
    {
        comp.Active = !comp.Active;
        Dirty(uid, comp);

        if (comp.SoundEntity != null)
            _volume.SetMuted(uid, !_power.IsPowered(uid) || !comp.Active);
    }

    private void OnMediaPlayed(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaPlayedEvent args)
    {
        var audio = _audio.PlayPredicted(args.MediaPlayed, uid, uid, _volume.WithVolume(uid, comp.DefaultParams));
        if (audio == null)
            return;

        comp.SoundEntity = audio.Value.Entity;
        _volume.SetStream(uid, comp.SoundEntity, comp.DefaultParams.Volume);
        Dirty(uid, comp);

        _volume.SetMuted(uid, !_power.IsPowered(uid) || !comp.Active);
    }

    private void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
        if (comp.SoundEntity == null)
            return;

        _volume.ClearStream(uid, comp.SoundEntity);
        comp.SoundEntity = _audio.Stop(comp.SoundEntity);
        Dirty(uid, comp);
    }
}
