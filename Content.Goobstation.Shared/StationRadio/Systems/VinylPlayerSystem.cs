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
using Robust.Shared.Audio.Systems;
using Robust.Shared.Containers;
using Robust.Shared.Maths;
using Robust.Shared.Network;
using Robust.Shared.Utility;

namespace Content.Goobstation.Shared.StationRadio.Systems;

public sealed partial class VinylPlayerSystem : EntitySystem // CorvaxGoob Edit - made partial
{
    private static readonly SpriteSpecifier VolumeVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png"));

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

        var audio = _audio.PlayPredicted(vinylcomp.Song, uid, uid, comp.AudioParams.WithVolume(GetVinylVolume(comp)));
        if (audio != null)
        {
            comp.SoundEntity = audio.Value.Entity;
            Dirty(uid, comp);
        }

        // Used by VinylSummonRuleSystem
        var ev = new VinylInsertedEvent(args.Entity);
        RaiseLocalEvent(uid, ref ev);

        if (!CheckForRadioRig(uid))
            return;

        // CorvaxGoob - CassetteRadio
        if (!TryStartCurrentRadioMedia(vinylcomp.Song))
            return;

        var query = EntityQueryEnumerator<StationRadioReceiverComponent>();
        while (query.MoveNext(out var receiver, out var receiverComponent))
        {
            if (!receiverComponent.SoundEntity.HasValue)
                RaiseLocalEvent(receiver, new StationRadioMediaPlayedEvent(vinylcomp.Song));
        }

        PlayCassetteRadioMedia(vinylcomp.Song); // CorvaxGoob - CassetteRadio
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
        if (!args.CanAccess || !args.CanInteract)
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("station-radio-volume-verb"),
            Icon = VolumeVerbIcon,
            Act = () => OpenVolumeUi(ent, user),
        });
    }

    private void OnSetVolume(Entity<VinylPlayerComponent> ent, ref StationRadioSetVolumeMessage args)
    {
        if (args.Actor is not { Valid: true })
            return;

        ent.Comp.Volume = MathHelper.Clamp(args.Volume, 0f, 1f);
        Dirty(ent.Owner, ent.Comp);

        if (ent.Comp.SoundEntity != null)
            _audio.SetVolume(ent.Comp.SoundEntity, GetVinylVolume(ent.Comp));

        UpdateVolumeUi(ent);
    }

    private void OpenVolumeUi(Entity<VinylPlayerComponent> ent, EntityUid user)
    {
        UpdateVolumeUi(ent);
        _ui.TryOpenUi(ent.Owner, StationRadioVolumeUiKey.Key, user);
    }

    private void UpdateVolumeUi(Entity<VinylPlayerComponent> ent)
    {
        _ui.SetUiState(ent.Owner, StationRadioVolumeUiKey.Key, new StationRadioVolumeState(ent.Comp.Volume));
    }

    /// <summary>
    /// Combines the record player's base local audio volume with the per-device slider multiplier.
    /// </summary>
    private static float GetVinylVolume(VinylPlayerComponent comp)
    {
        return comp.AudioParams.Volume + SharedAudioSystem.GainToVolume(comp.Volume);
    }
}
