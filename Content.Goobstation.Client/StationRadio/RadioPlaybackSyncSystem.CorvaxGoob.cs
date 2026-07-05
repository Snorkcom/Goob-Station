using System.Collections.Generic;
using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Robust.Shared.Audio.Components;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.StationRadio;

public sealed class RadioPlaybackSyncSystem : EntitySystem
{
    private static readonly TimeSpan PendingAutoSyncLifetime = TimeSpan.FromSeconds(2);

    [Dependency] private readonly IGameTiming _timing = default!;

    private readonly Dictionary<EntityUid, TimeSpan> _pendingAutoSync = new();
    private readonly List<EntityUid> _pendingAutoSyncRemoval = new();

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<RadioSyncedAudioComponent, ComponentStartup>(OnRadioSyncedAudioStartup);
        SubscribeLocalEvent<RadioSyncedAudioComponent, ComponentShutdown>(OnRadioSyncedAudioShutdown);
        SubscribeLocalEvent<AudioComponent, ComponentStartup>(OnAudioStartup);
        SubscribeNetworkEvent<RadioPlaybackResyncEvent>(OnRadioPlaybackResync);
    }

    private void OnRadioSyncedAudioStartup(EntityUid uid, RadioSyncedAudioComponent component, ComponentStartup args)
    {
        QueueAutoSync(uid);
    }

    private void OnRadioSyncedAudioShutdown(EntityUid uid, RadioSyncedAudioComponent component, ComponentShutdown args)
    {
        _pendingAutoSync.Remove(uid);
    }

    private void OnAudioStartup(EntityUid uid, AudioComponent component, ComponentStartup args)
    {
        if (HasComp<RadioSyncedAudioComponent>(uid))
            QueueAutoSync(uid);
    }

    public override void Update(float frameTime)
    {
        base.Update(frameTime);

        if (_pendingAutoSync.Count == 0)
            return;

        var now = _timing.CurTime;
        _pendingAutoSyncRemoval.Clear();

        foreach (var (uid, expiresAt) in _pendingAutoSync)
        {
            if (expiresAt <= now ||
                TerminatingOrDeleted(uid) ||
                !HasComp<RadioSyncedAudioComponent>(uid))
            {
                _pendingAutoSyncRemoval.Add(uid);
                continue;
            }

            if (!TryComp(uid, out AudioComponent? targetAudio) ||
                !IsReadyRadioAudio(targetAudio))
            {
                continue;
            }

            if (!TryGetCassetteMaster(uid, out var masterAudio))
                continue;

            // Local-only alignment: new radio streams follow the first already playing cassette stream.
            targetAudio.PlaybackPosition = masterAudio.PlaybackPosition;
            _pendingAutoSyncRemoval.Add(uid);
        }

        foreach (var uid in _pendingAutoSyncRemoval)
        {
            _pendingAutoSync.Remove(uid);
        }
    }

    private void OnRadioPlaybackResync(RadioPlaybackResyncEvent msg)
    {
        var query = EntityQueryEnumerator<RadioSyncedAudioComponent, AudioComponent>();
        while (query.MoveNext(out _, out _, out var audio))
        {
            ResyncAudio(audio);
        }
    }

    private void QueueAutoSync(EntityUid uid)
    {
        // The marker and AudioComponent can arrive on different frames, so retry briefly.
        _pendingAutoSync[uid] = _timing.CurTime + PendingAutoSyncLifetime;
    }

    private bool TryGetCassetteMaster(EntityUid except, out AudioComponent audio)
    {
        var query = EntityQueryEnumerator<RadioSyncedAudioComponent, AudioComponent>();
        while (query.MoveNext(out var uid, out _, out var candidate))
        {
            if (uid == except || !candidate.Global || !IsReadyRadioAudio(candidate))
                continue;

            audio = candidate;
            return true;
        }

        audio = default!;
        return false;
    }

    private static bool IsReadyRadioAudio(AudioComponent audio)
    {
        return audio.Loaded &&
            audio.Started &&
            audio.State == AudioState.Playing;
    }

    private void ResyncAudio(AudioComponent audio)
    {
        if (audio.State != AudioState.Playing)
            return;

        // Local-only resync: force the source to the position implied by its networked AudioStart.
        var expectedOffset = MathF.Max(0f, (float) ((audio.PauseTime ?? _timing.CurTime) - audio.AudioStart).TotalSeconds);
        audio.PlaybackPosition = expectedOffset;
    }
}
