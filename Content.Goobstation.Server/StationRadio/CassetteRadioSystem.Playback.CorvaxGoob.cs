using Content.Goobstation.Shared.StationRadio.Components;
using Content.Goobstation.Shared.StationRadio.Events;
using Robust.Shared.Audio;
using Robust.Shared.Audio.Components;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Player;

namespace Content.Goobstation.Server.StationRadio;

public sealed partial class CassetteRadioSystem
{
    private void OnShutdown(Entity<CassetteRadioComponent> ent, ref ComponentShutdown args)
    {
        StopMedia(ent);
    }

    private void OnMediaPlayed(Entity<CassetteRadioComponent> ent, ref StationRadioMediaPlayedEvent args)
    {
        StopMedia(ent);
        // Personal streams query the clock at creation so reconnects and slot moves use the freshest offset.
        TryStartCurrentMedia(ent);
    }

    private void OnMediaStopped(Entity<CassetteRadioComponent> ent, ref StationRadioMediaStoppedEvent args)
    {
        StopMedia(ent);
    }

    private void TryStartCurrentMedia(Entity<CassetteRadioComponent> ent)
    {
        if (!ent.Comp.Active || !TryGetWearerActor(ent, out _, out var actor))
            return;

        if (ent.Comp.SoundEntity != null)
        {
            if (ent.Comp.SoundSession != actor.PlayerSession)
            {
                StopMedia(ent);
            }
            else
            {
                SetMediaGain(ent, true);
                return;
            }
        }

        if (!_vinylPlayer.TryGetCurrentRadioMedia(out var media))
            return;

        StartMedia(ent, media.Media, _vinylPlayer.GetCurrentRadioMediaOffset(media));
    }

    /// <summary>
    /// Recreates personal global audio after the client gets a fresh player session.
    /// </summary>
    private void OnPlayerAttached(PlayerAttachedEvent args)
    {
        foreach (var cassette in GetCarriedCassettes(args.Entity))
        {
            cassette.Comp.Wearer = args.Entity;
            RefreshRadioReceiver(cassette);
            TryStartCurrentMedia(cassette);
        }
    }

    /// <summary>
    /// Clears session-bound audio before the old player session disappears.
    /// </summary>
    private void OnPlayerDetached(PlayerDetachedEvent args)
    {
        foreach (var cassette in GetCarriedCassettes(args.Entity))
        {
            if (cassette.Comp.Wearer != args.Entity)
                continue;

            StopMedia(cassette);
            RefreshRadioReceiver(cassette);
        }
    }

    private void StartMedia(Entity<CassetteRadioComponent> ent, SoundPathSpecifier media, float offset)
    {
        if (!ent.Comp.Active || ent.Comp.SoundEntity != null)
            return;

        if (!TryGetWearerActor(ent, out _, out var actor))
            return;

        var resolved = _audio.ResolveSound(media);
        if (_audio.GetAudioLength(resolved).TotalSeconds <= offset)
            return;

        var audio = _audio.PlayGlobal(resolved, actor.PlayerSession, ent.Comp.AudioParams
            .WithPlayOffset(offset)
            .WithVolume(GetRadioVolume(ent.Comp)));
        if (audio == null)
            return;

        ent.Comp.SoundEntity = audio.Value.Entity;
        ent.Comp.SoundSession = actor.PlayerSession;
        // Keep the server-side AudioStart aligned for clients that receive this stream after it was created.
        _audio.SetPlaybackPosition(new Entity<AudioComponent?>(audio.Value.Entity, audio.Value.Component), offset);

        EnsureComp<RadioSyncedAudioComponent>(audio.Value.Entity);
    }

    private void SetMediaGain(Entity<CassetteRadioComponent> ent, bool audible)
    {
        if (ent.Comp.SoundEntity == null)
            return;

        if (audible)
            _audio.SetVolume(ent.Comp.SoundEntity, GetRadioVolume(ent.Comp));
        else
            _audio.SetGain(ent.Comp.SoundEntity, 0f);
    }

    private static float GetRadioVolume(CassetteRadioComponent comp)
    {
        return comp.AudioParams.Volume + SharedAudioSystem.GainToVolume(comp.Volume);
    }

    private void StopMedia(Entity<CassetteRadioComponent> ent)
    {
        if (ent.Comp.SoundEntity == null)
        {
            ent.Comp.SoundSession = null;
            return;
        }

        ent.Comp.SoundEntity = _audio.Stop(ent.Comp.SoundEntity);
        ent.Comp.SoundSession = null;
    }
}
