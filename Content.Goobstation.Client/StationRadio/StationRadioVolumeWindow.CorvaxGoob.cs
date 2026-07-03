using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Range = Robust.Client.UserInterface.Controls.Range;

namespace Content.Goobstation.Client.StationRadio;

public sealed class StationRadioVolumeWindow : DefaultWindow
{
    private readonly Label _volumeLabel;
    private readonly Slider _volumeSlider;

    public event Action<float>? OnVolumeChanged;

    public StationRadioVolumeWindow()
    {
        Title = Loc.GetString("station-radio-volume-window-title");
        MinSize = new Vector2(300, 96);

        var container = new BoxContainer
        {
            Orientation = BoxContainer.LayoutOrientation.Vertical,
            Margin = new Thickness(8),
        };

        Contents.AddChild(container);

        _volumeLabel = new Label();
        container.AddChild(_volumeLabel);

        _volumeSlider = new Slider
        {
            MinValue = 0f,
            MaxValue = 100f,
            Rounded = true,
            RoundingDecimals = 0,
            HorizontalExpand = true,
        };

        _volumeSlider.OnValueChanged += OnSliderValueChanged;
        container.AddChild(_volumeSlider);

        SetVolume(1f);
    }

    public void SetVolume(float volume)
    {
        var percent = Math.Clamp(volume, 0f, 1f) * 100f;
        _volumeSlider.SetValueWithoutEvent(percent);
        UpdateLabel(percent);
    }

    private void OnSliderValueChanged(Range range)
    {
        UpdateLabel(range.Value);
        OnVolumeChanged?.Invoke(range.Value / 100f);
    }

    private void UpdateLabel(float percent)
    {
        _volumeLabel.Text = Loc.GetString("station-radio-volume-window-volume",
            ("volume", (int) MathF.Round(percent)));
    }
}
