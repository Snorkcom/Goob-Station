using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Content.Goobstation.Shared.StationRadio;
using Content.Shared.Interaction;
using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;

namespace Content.Goobstation.Shared.StationRadio.Systems;

public sealed class StationRadioReceiverSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    public override void Initialize()
    {
        base.Initialize();
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaPlayedEvent>(OnMediaPlayed);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioMediaStoppedEvent>(OnMediaStopped);
        SubscribeLocalEvent<StationRadioReceiverComponent, ActivateInWorldEvent>(OnRadioToggle);
        SubscribeLocalEvent<StationRadioReceiverComponent, PowerChangedEvent>(OnPowerChanged);
        SubscribeLocalEvent<StationRadioReceiverComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<StationRadioReceiverComponent, StationRadioSetVolumeMessage>(OnSetVolume);
    }

    private void OnPowerChanged(EntityUid uid, StationRadioReceiverComponent comp, PowerChangedEvent args)
    {
        if (!args.Powered)
            StationRadioVolumeHelpers.CloseVolumeUiIfUnpowered(uid, _power, _ui);

        if (comp.SoundEntity == null)
            return;

        SetReceiverAudible(comp, args.Powered && comp.Active);
    }

    private void OnRadioToggle(EntityUid uid, StationRadioReceiverComponent comp, ActivateInWorldEvent args)
    {
        comp.Active = !comp.Active;
        Dirty(uid, comp);

        if (comp.SoundEntity != null)
            SetReceiverAudible(comp, _power.IsPowered(uid) && comp.Active);
    }

    private void OnMediaPlayed(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaPlayedEvent args)
    {
        var audio = _audio.PlayPredicted(args.MediaPlayed, uid, uid, comp.DefaultParams
            .WithVolume(GetReceiverVolume(comp)));
        if (audio == null)
            return;

        comp.SoundEntity = audio.Value.Entity;
        Dirty(uid, comp);

        if (!_power.IsPowered(uid) || !comp.Active)
        {
            _audio.SetGain(comp.SoundEntity, 0);
        }
    }

    private void OnMediaStopped(EntityUid uid, StationRadioReceiverComponent comp, StationRadioMediaStoppedEvent args)
    {
        if (comp.SoundEntity == null)
            return;

        comp.SoundEntity = _audio.Stop(comp.SoundEntity);
        Dirty(uid, comp);
    }

    private void OnGetVerbs(Entity<StationRadioReceiverComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        StationRadioVolumeHelpers.AddVolumeVerb(ent, ref args, _power, OpenVolumeUi);
    }

    private void OnSetVolume(Entity<StationRadioReceiverComponent> ent, ref StationRadioSetVolumeMessage args)
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
            SetReceiverAudible(ent.Comp, _power.IsPowered(ent.Owner) && ent.Comp.Active);

        StationRadioVolumeHelpers.UpdateVolumeUi(ent.Owner, ent.Comp.Volume, _ui);
    }

    private void OpenVolumeUi(Entity<StationRadioReceiverComponent> ent, EntityUid user)
    {
        StationRadioVolumeHelpers.OpenVolumeUi(ent.Owner, user, ent.Comp.Volume, _power, _ui);
    }

    /// <summary>
    /// Applies the slider only to the receiver's current music stream; power/toggle mute still uses gain 0.
    /// </summary>
    private void SetReceiverAudible(StationRadioReceiverComponent comp, bool audible)
    {
        if (audible)
            _audio.SetVolume(comp.SoundEntity, GetReceiverVolume(comp));
        else
            _audio.SetGain(comp.SoundEntity, 0f);
    }

    /// <summary>
    /// Combines the prototype's base volume with the per-device slider multiplier.
    /// </summary>
    private static float GetReceiverVolume(StationRadioReceiverComponent comp)
    {
        return comp.DefaultParams.Volume + SharedAudioSystem.GainToVolume(comp.Volume);
    }
}
