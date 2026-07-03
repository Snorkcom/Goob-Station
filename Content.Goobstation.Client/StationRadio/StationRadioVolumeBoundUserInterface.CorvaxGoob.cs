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
        _window.OnVolumeChanged += volume => SendPredictedMessage(new StationRadioSetVolumeMessage(volume));
    }

    protected override void UpdateState(BoundUserInterfaceState state)
    {
        base.UpdateState(state);

        if (state is not StationRadioVolumeState cast)
            return;

        _window?.SetVolume(cast.Volume);
    }
}
