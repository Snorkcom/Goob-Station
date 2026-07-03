using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Robust.Shared.Audio.Components;
using Robust.Shared.Timing;

namespace Content.Goobstation.Client.StationRadio;

public sealed class RadioPlaybackSyncSystem : EntitySystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeNetworkEvent<RadioPlaybackResyncEvent>(OnRadioPlaybackResync);
    }

    private void OnRadioPlaybackResync(RadioPlaybackResyncEvent msg)
    {
        var query = EntityQueryEnumerator<RadioSyncedAudioComponent, AudioComponent>();
        while (query.MoveNext(out _, out _, out var audio))
        {
            ResyncAudio(audio);
        }
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
