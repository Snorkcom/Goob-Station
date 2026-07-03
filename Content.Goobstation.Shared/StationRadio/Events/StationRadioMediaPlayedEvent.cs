using Robust.Shared.Audio;
using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.StationRadio.Events;

[Serializable, NetSerializable]
public sealed class StationRadioMediaPlayedEvent : EntityEventArgs
{
    public SoundPathSpecifier MediaPlayed { get; }

    /// <summary>
    /// Offset from the station radio clock used to align late-created spatial audio streams.
    /// </summary>
    public float PlayOffset { get; }

    /// <summary>
    /// Station-wide broadcast clock metadata copied onto radio audio marker components.
    /// </summary>
    public TimeSpan BroadcastStartTime { get; }

    public StationRadioMediaPlayedEvent(SoundPathSpecifier media, float playOffset = 0f, TimeSpan broadcastStartTime = default)
    {
        MediaPlayed = media;
        PlayOffset = playOffset;
        BroadcastStartTime = broadcastStartTime;
    }
}
