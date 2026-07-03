using Content.Goobstation.Shared.StationRadio;
using Robust.Client.UserInterface;

namespace Content.Goobstation.Client.StationRadio;

public sealed class StationRadioVolumeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private const float VolumeAckTolerance = 0.001f;

    private StationRadioVolumeWindow? _window;
    private float? _awaitingVolumeAck;

    protected override void Open()
    {
        base.Open();

        _awaitingVolumeAck = null;
        _window = this.CreateWindow<StationRadioVolumeWindow>();
        _window.OnVolumeCommitted += OnVolumeCommitted;
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not StationRadioVolumeState cast)
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
        SendPredictedMessage(new StationRadioSetVolumeMessage(volume));
    }
}
