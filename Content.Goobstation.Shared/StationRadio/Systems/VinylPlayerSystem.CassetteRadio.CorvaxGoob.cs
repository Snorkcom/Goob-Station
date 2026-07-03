using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Robust.Shared.Audio;
using Robust.Shared.Timing;

namespace Content.Goobstation.Shared.StationRadio.Systems;

public sealed partial class VinylPlayerSystem
{
    [Dependency] private readonly IGameTiming _timing = default!;

    private StationRadioMedia? _currentRadioMedia;

    private bool TryStartCurrentRadioMedia(SoundPathSpecifier media)
    {
        if (_currentRadioMedia != null)
            return false;

        _currentRadioMedia = new StationRadioMedia(media, _timing.CurTime);
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
        return MathF.Max(0f, (float) (_timing.CurTime - media.StartTime).TotalSeconds);
    }

    private void PlayCassetteRadioMedia(SoundPathSpecifier media)
    {
        var cassetteQuery = EntityQueryEnumerator<CassetteRadioComponent>();
        while (cassetteQuery.MoveNext(out var cassette, out _))
        {
            RaiseLocalEvent(cassette, new StationRadioMediaPlayedEvent(media));
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

public readonly record struct StationRadioMedia(SoundPathSpecifier Media, TimeSpan StartTime);
