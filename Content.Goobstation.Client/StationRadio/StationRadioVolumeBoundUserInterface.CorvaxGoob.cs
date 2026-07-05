using Content.Goobstation.Shared.StationRadio;
using Robust.Client.UserInterface;

namespace Content.Goobstation.Client.StationRadio;

public sealed class StationRadioVolumeBoundUserInterface(EntityUid owner, Enum uiKey) : BoundUserInterface(owner, uiKey)
{
    private StationRadioVolumeWindow? _window;

    protected override void Open()
    {
        base.Open();

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

        _window.SetVolume(cast.Volume);
    }

    private void OnVolumeCommitted(float volume)
    {
        SendMessage(new StationRadioSetVolumeMessage(volume));
    }
}
