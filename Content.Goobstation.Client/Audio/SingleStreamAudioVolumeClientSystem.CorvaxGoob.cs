using Content.Goobstation.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.GameStates;
using Robust.Shared.Maths;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.Audio;

public sealed class SingleStreamAudioVolumeClientSystem : EntitySystem
{
    private const float VolumeAckTolerance = 0.001f;
    // If the server never confirms our local prediction, restore the old value after this delay.
    private const float PendingVolumeTimeout = 2f;

    [Dependency] private readonly SharedAudioSystem _audio = default!;
    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, PredictedVolume> _predictedVolumes = new();

    private readonly record struct PredictedVolume(float Volume, float PreviousVolume, TimeSpan ExpiresAt);

    public override void Initialize()
    {
        base.Initialize();

        UpdatesOutsidePrediction = true;
        UpdatesAfter.Add(typeof(Robust.Client.Audio.AudioSystem));

        SubscribeLocalEvent<SingleStreamAudioVolumeComponent, ComponentStartup>(OnVolumeStartup);
        SubscribeLocalEvent<SingleStreamAudioVolumeComponent, AfterAutoHandleStateEvent>(OnVolumeState);
        SubscribeLocalEvent<SingleStreamAudioVolumeComponent, ComponentShutdown>(OnVolumeShutdown);
    }

    public override void FrameUpdate(float frameTime)
    {
        // AudioSystem copies AudioComponent.Params into the playing source every update.
        // Run after it and keep our locally controlled stream volume applied.
        var query = AllEntityQuery<SingleStreamAudioVolumeComponent>();
        while (query.MoveNext(out var uid, out var component))
        {
            KeepPredictedVolume(uid, component);
            ApplyLocalVolume(component);
        }
    }

    public void ApplyPredictedVolume(EntityUid uid, float volume, SingleStreamAudioVolumeComponent? component = null)
    {
        if (!Resolve(uid, ref component, false))
            return;

        volume = MathHelper.Clamp(volume, 0f, 1f);

        // Remember the old value so a rejected or lost server message cannot leave
        // the client stuck with a local-only volume forever.
        var previousVolume = _predictedVolumes.TryGetValue(uid, out var prediction)
            ? prediction.PreviousVolume
            : component.Volume;

        _predictedVolumes[uid] = new PredictedVolume(
            volume,
            previousVolume,
            _timing.CurTime + TimeSpan.FromSeconds(PendingVolumeTimeout));

        component.Volume = volume;
        ApplyLocalVolume(component);
    }

    private void OnVolumeStartup(Entity<SingleStreamAudioVolumeComponent> ent, ref ComponentStartup args)
    {
        ApplyLocalVolume(ent.Comp);
    }

    private void OnVolumeState(Entity<SingleStreamAudioVolumeComponent> ent, ref AfterAutoHandleStateEvent args)
    {
        ReconcilePredictedVolume(ent.Owner, ent.Comp);
        ApplyLocalVolume(ent.Comp);
    }

    private void OnVolumeShutdown(Entity<SingleStreamAudioVolumeComponent> ent, ref ComponentShutdown args)
    {
        _predictedVolumes.Remove(ent.Owner);
    }

    private void ReconcilePredictedVolume(EntityUid uid, SingleStreamAudioVolumeComponent component)
    {
        if (!_predictedVolumes.TryGetValue(uid, out var prediction))
            return;

        // The server has echoed our value back, so the prediction is no longer needed.
        if (MathF.Abs(component.Volume - prediction.Volume) <= VolumeAckTolerance)
        {
            _predictedVolumes.Remove(uid);
            return;
        }

        // Server sent a different value. Keep our prediction briefly, but remember
        // the server value so timeout restores the real shared state.
        _predictedVolumes[uid] = prediction with { PreviousVolume = component.Volume };
        KeepPredictedVolume(uid, component);
    }

    private void KeepPredictedVolume(EntityUid uid, SingleStreamAudioVolumeComponent component)
    {
        if (!_predictedVolumes.TryGetValue(uid, out var prediction))
            return;

        // Safety net: if the server rejects or loses the message, do not keep a local-only value forever.
        if (_timing.CurTime >= prediction.ExpiresAt)
        {
            _predictedVolumes.Remove(uid);
            component.Volume = prediction.PreviousVolume;
            return;
        }

        component.Volume = prediction.Volume;
    }

    private void ApplyLocalVolume(SingleStreamAudioVolumeComponent component)
    {
        if (component.AudioStream == null || !TryComp(component.AudioStream, out AudioComponent? audio))
            return;

        ApplyLocalVolume(component, audio);
    }

    private void ApplyLocalVolume(SingleStreamAudioVolumeComponent component, AudioComponent audio)
    {
        var volume = component.Muted
            ? SharedAudioSystem.GainToVolume(0f)
            : SingleStreamAudioVolumeSystem.GetEffectiveVolume(component);

        // Keep the client-local AudioComponent params in sync so AudioSystem does not snap
        // the source back to its server-created base volume on the next audio update.
        _audio.SetVolume(component.AudioStream, volume, audio);
    }
}
