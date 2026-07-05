using Content.Goobstation.Shared.Audio;
using Robust.Client.UserInterface;

namespace Content.Goobstation.Client.Audio;

public sealed class SingleStreamAudioVolumeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private const float VolumeAckTolerance = 0.001f;

    private SingleStreamAudioVolumeWindow? _window;
    private float? _awaitingVolumeAck;

    protected override void Open()
    {
        base.Open();

        _awaitingVolumeAck = null;
        _window = this.CreateWindow<SingleStreamAudioVolumeWindow>();
        _window.OnVolumeCommitted += OnVolumeCommitted;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not SingleStreamAudioVolumeState cast)
            return;

        if (_window == null || _window.IsDragging)
            return;

        // Old server states can arrive after the release message; keep the local value until the server echoes it.
        if (_awaitingVolumeAck is { } awaitingVolume)
        {
            if (MathF.Abs(cast.Volume - awaitingVolume) > VolumeAckTolerance)
                return;

            _awaitingVolumeAck = null;
        }

        _window.SetVolume(cast.Volume);
    }

    private void OnVolumeCommitted(float volume)
    {
        _awaitingVolumeAck = volume;
        // Apply immediately on this client so the audio does not jump while waiting for server state.
        EntMan.System<SingleStreamAudioVolumeClientSystem>().ApplyPredictedVolume(Owner, volume);
        SendPredictedMessage(new SingleStreamAudioVolumeSetMessage(volume));
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);

        if (!disposing || _window == null)
            return;

        _window.OnVolumeCommitted -= OnVolumeCommitted;
        _window = null;
    }
}
