using Content.Shared.Power;
using Content.Shared.Power.EntitySystems;
using Content.Shared.UserInterface;
using Content.Shared.Verbs;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Maths;
using Robust.Shared.Utility;

namespace Content.Goobstation.Shared.Audio;

public sealed class SingleStreamAudioVolumeSystem : EntitySystem
{
    // 0% is full mute. Any non-zero slider value maps into this bounded dB range.
    private const float MinVolumeDb = -18f;

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly SharedPowerReceiverSystem _power = default!;
    [Dependency] private readonly SharedUserInterfaceSystem _ui = default!;

    private static readonly SpriteSpecifier VolumeVerbIcon =
        new SpriteSpecifier.Texture(new ResPath("/Textures/Interface/VerbIcons/settings.svg.192dpi.png"));

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<SingleStreamAudioVolumeComponent, GetVerbsEvent<AlternativeVerb>>(OnGetVerbs);
        SubscribeLocalEvent<SingleStreamAudioVolumeComponent, SingleStreamAudioVolumeSetMessage>(OnSetVolume);
        SubscribeLocalEvent<SingleStreamAudioVolumeComponent, PowerChangedEvent>(OnPowerChanged);
    }

    /// <summary>
    /// Applies the saved slider volume to params before a new controlled stream is created.
    /// </summary>
    public AudioParams WithVolume(EntityUid uid, AudioParams audioParams, SingleStreamAudioVolumeComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return audioParams;

        return audioParams.WithVolume(GetEffectiveVolume(component, audioParams.Volume));
    }

    /// <summary>
    /// Registers the one audio stream this entity currently controls.
    /// </summary>
    public void SetStream(EntityUid uid, EntityUid? stream, float baseVolume, SingleStreamAudioVolumeComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (stream == null)
        {
            ClearStream(uid, component: component);
            return;
        }

        component.AudioStream = stream;
        component.BaseVolume = baseVolume;
        Dirty(uid, component);

        ApplyServerMuteIfNeeded(component);
        UpdateVolumeUi(uid, component);
    }

    /// <summary>
    /// Clears the registered stream when the owner stops or replaces its music.
    /// </summary>
    public void ClearStream(EntityUid uid, EntityUid? stream = null, SingleStreamAudioVolumeComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (component.AudioStream == null)
            return;

        if (stream != null && component.AudioStream != stream)
            return;

        component.AudioStream = null;
        Dirty(uid, component);

        UpdateVolumeUi(uid, component);
    }

    /// <summary>
    /// Temporarily silences or restores the current stream for power/off state changes.
    /// </summary>
    public void SetMuted(EntityUid uid, bool muted, SingleStreamAudioVolumeComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        if (component.Muted == muted)
            return;

        component.Muted = muted;
        Dirty(uid, component);

        ApplyServerMutedStateChange(component);
    }

    private void OnGetVerbs(Entity<SingleStreamAudioVolumeComponent> ent, ref GetVerbsEvent<AlternativeVerb> args)
    {
        if (!args.CanAccess || !args.CanInteract)
            return;

        if (!IsAvailable(ent.Owner, ent.Comp))
            return;

        var user = args.User;
        args.Verbs.Add(new AlternativeVerb
        {
            Text = Loc.GetString("single-stream-audio-volume-verb"),
            Icon = VolumeVerbIcon,
            // Keep item-slot eject and other default alternative verbs above volume in alt-click priority.
            Priority = -1,
            Act = () => OpenVolumeUi(ent.Owner, user, ent.Comp),
        });
    }

    private void OnSetVolume(Entity<SingleStreamAudioVolumeComponent> ent, ref SingleStreamAudioVolumeSetMessage args)
    {
        if (args.Actor is not { Valid: true })
            return;

        if (!_ui.IsUiOpen(ent.Owner, SingleStreamAudioVolumeUiKey.Key, args.Actor))
            return;

        if (!IsAvailable(ent.Owner, ent.Comp))
        {
            CloseVolumeUi(ent.Owner);
            return;
        }

        ent.Comp.Volume = MathHelper.Clamp(args.Volume, 0f, 1f);
        Dirty(ent.Owner, ent.Comp);

        // Do not call Audio.SetVolume on the live stream here. Clients apply this value
        // locally so AudioComponent playback position is not corrected when volume changes.
        UpdateVolumeUi(ent.Owner, ent.Comp);
    }

    private void OnPowerChanged(Entity<SingleStreamAudioVolumeComponent> ent, ref PowerChangedEvent args)
    {
        if (ent.Comp.RequiresPower && !args.Powered)
            CloseVolumeUi(ent.Owner);
    }

    private bool IsAvailable(EntityUid uid, SingleStreamAudioVolumeComponent component)
    {
        return !component.RequiresPower || _power.IsPowered(uid);
    }

    private void OpenVolumeUi(EntityUid uid, EntityUid user, SingleStreamAudioVolumeComponent component)
    {
        if (!IsAvailable(uid, component))
        {
            CloseVolumeUi(uid);
            return;
        }

        UpdateVolumeUi(uid, component);
        _ui.TryOpenUi(uid, SingleStreamAudioVolumeUiKey.Key, user);
    }

    private void CloseVolumeUi(EntityUid uid)
    {
        _ui.CloseUi(uid, SingleStreamAudioVolumeUiKey.Key);
    }

    private void UpdateVolumeUi(EntityUid uid, SingleStreamAudioVolumeComponent component)
    {
        _ui.SetUiState(uid, SingleStreamAudioVolumeUiKey.Key, new SingleStreamAudioVolumeState(component.Volume));
    }

    private void ApplyServerMutedStateChange(SingleStreamAudioVolumeComponent component)
    {
        if (component.AudioStream == null)
            return;

        if (component.Muted)
        {
            _audio.SetGain(component.AudioStream, 0f);
            return;
        }

        _audio.SetVolume(component.AudioStream, GetEffectiveVolume(component));
    }

    private void ApplyServerMuteIfNeeded(SingleStreamAudioVolumeComponent component)
    {
        if (!component.Muted || component.AudioStream == null)
            return;

        _audio.SetGain(component.AudioStream, 0f);
    }

    public static float GetEffectiveVolume(SingleStreamAudioVolumeComponent component)
    {
        return GetEffectiveVolume(component, component.BaseVolume);
    }

    public static float GetEffectiveVolume(SingleStreamAudioVolumeComponent component, float baseVolume)
    {
        var volume = MathHelper.Clamp(component.Volume, 0f, 1f);
        if (volume <= 0f)
            return float.NegativeInfinity;

        // Use dB values directly so low volume does not become unstable tiny gain values.
        return baseVolume + MathHelper.Lerp(MinVolumeDb, 0f, volume);
    }
}
