using System.Collections.Generic;
using Content.Goobstation.Shared.StationRadio.Components;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;

namespace Content.Goobstation.Client.StationRadio;

public sealed class RadioPlaybackSyncSystem : EntitySystem
{
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    private readonly Dictionary<EntityUid, float> _mutedSpatialStreams = new();
    private readonly List<EntityUid> _staleMutedStreams = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioSyncedAudioComponent, ComponentStartup>(OnRadioSyncedAudioStartup);
        SubscribeLocalEvent<RadioSyncedAudioComponent, ComponentShutdown>(OnRadioSyncedAudioShutdown);
    }

    private void OnRadioSyncedAudioStartup(EntityUid uid, RadioSyncedAudioComponent component, ComponentStartup args)
    {
        RefreshRadioMuting();
    }

    private void OnRadioSyncedAudioShutdown(EntityUid uid, RadioSyncedAudioComponent component, ComponentShutdown args)
    {
        _mutedSpatialStreams.Remove(uid);
        RefreshRadioMuting();
    }

    private void RefreshRadioMuting()
    {
        var muteSpatialStreams = HasActiveCassetteStream();
        CleanupMutedStreams();

        var query = EntityQueryEnumerator<RadioSyncedAudioComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out _, out var audio))
        {
            if (TerminatingOrDeleted(uid) || audio.Global)
                continue;

            if (muteSpatialStreams)
                MuteSpatialStream(uid, audio);
            else
                RestoreSpatialStream(uid, audio);
        }
    }

    private bool HasActiveCassetteStream()
    {
        var query = EntityQueryEnumerator<RadioSyncedAudioComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out _, out var audio))
        {
            if (TerminatingOrDeleted(uid) || !audio.Global || !IsReadyRadioAudio(audio))
                continue;

            return true;
        }

        return false;
    }

    private static bool IsReadyRadioAudio(AudioComponent audio)
    {
        return audio.Loaded &&
            audio.Started &&
            audio.State == AudioState.Playing;
    }

    private void MuteSpatialStream(EntityUid uid, AudioComponent audio)
    {
        if (!float.IsNegativeInfinity(audio.Params.Volume))
        {
            _mutedSpatialStreams[uid] = audio.Params.Volume;
            _audio.SetVolume(uid, float.NegativeInfinity, audio);
            return;
        }

        _mutedSpatialStreams.TryAdd(uid, audio.Params.Volume);
    }

    private void RestoreSpatialStream(EntityUid uid, AudioComponent audio)
    {
        if (!_mutedSpatialStreams.Remove(uid, out var volume))
            return;

        _audio.SetVolume(uid, volume, audio);
    }

    private void CleanupMutedStreams()
    {
        _staleMutedStreams.Clear();

        foreach (var uid in _mutedSpatialStreams.Keys)
        {
            if (TerminatingOrDeleted(uid) ||
                !HasComp<RadioSyncedAudioComponent>(uid) ||
                !HasComp<AudioComponent>(uid))
            {
                _staleMutedStreams.Add(uid);
            }
        }

        foreach (var uid in _staleMutedStreams)
        {
            _mutedSpatialStreams.Remove(uid);
        }
    }
}
