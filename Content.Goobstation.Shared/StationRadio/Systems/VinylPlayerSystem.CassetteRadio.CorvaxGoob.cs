using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.StationRadio.Systems;

public sealed partial class VinylPlayerSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    /// <summary>
    /// Single source of truth for the currently broadcast record and its station-wide start time.
    /// </summary>
    private StationRadioMedia? _currentRadioMedia;

    private bool TryStartCurrentRadioMedia(SoundPathSpecifier media, out StationRadioMedia radioMedia)
    {
        if (_currentRadioMedia != null)
        {
            // Return the active clock so callers can still align local audio even if they did not start the broadcast.
            radioMedia = _currentRadioMedia.Value;
            return false;
        }

        radioMedia = new StationRadioMedia(media, _timing.CurTime);
        _currentRadioMedia = radioMedia;
        return true;
    }

    private void StopCurrentRadioMedia()
    {
        StopCassetteRadioMedia();
        _currentRadioMedia = null;
    }

    public bool TryGetCurrentRadioMedia(out StationRadioMedia media)
    {
        if (_currentRadioMedia == null)
        {
            media = default;
            return false;
        }

        media = _currentRadioMedia.Value;
        return true;
    }

    public float GetCurrentRadioMediaOffset(StationRadioMedia media)
    {
        return GetRadioMediaOffset(media);
    }

    /// <summary>
    /// Converts the saved start time into the offset every late-created stream should seek to.
    /// </summary>
    private float GetRadioMediaOffset(StationRadioMedia media)
    {
        return MathF.Max(0f, (float) (_timing.CurTime - media.StartTime).TotalSeconds);
    }

    /// <summary>
    /// Tells every enabled personal cassette receiver to recreate its private stream from the broadcast clock.
    /// </summary>
    private void PlayCassetteRadioMedia(SoundPathSpecifier media, float playOffset)
    {
        var cassetteQuery = EntityQueryEnumerator<CassetteRadioComponent>();
        while (cassetteQuery.MoveNext(out var cassette, out _))
        {
            RaiseLocalEvent(cassette, new StationRadioMediaPlayedEvent(media, playOffset));
        }
    }

    private void StopCassetteRadioMedia()
    {
        var cassetteQuery = EntityQueryEnumerator<CassetteRadioComponent>();
        while (cassetteQuery.MoveNext(out var cassette, out _))
        {
            RaiseLocalEvent(cassette, new StationRadioMediaStoppedEvent());
        }
    }
}

/// <summary>
/// Describes the record currently being broadcast and when the broadcast clock started.
/// </summary>
public readonly record struct StationRadioMedia(SoundPathSpecifier Media, TimeSpan StartTime);
