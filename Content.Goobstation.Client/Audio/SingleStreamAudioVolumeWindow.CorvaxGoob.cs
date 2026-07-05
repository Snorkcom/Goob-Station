using System.Numerics;
using Robust.Client.UserInterface.Controls;
using Robust.Client.UserInterface.CustomControls;
using Robust.Shared.Maths;
using Range = Robust.Client.UserInterface.Controls.Range;

namespace Content.Goobstation.Client.Audio;

public sealed class SingleStreamAudioVolumeWindow : DefaultWindow
{
    private const float VolumeTolerance = 0.001f;

    private readonly Label _volumeLabel;
    private readonly Slider _volumeSlider;

    private float _pendingVolume = 1f;
    private float _committedVolume = 1f;

    public event Action<float>? OnVolumeCommitted;

    public bool IsDragging => _volumeSlider.Grabbed;

    public SingleStreamAudioVolumeWindow()
    {
        Title = Loc.GetString("single-stream-audio-volume-window-title");
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
        _volumeSlider.OnReleased += OnSliderReleased;
        container.AddChild(_volumeSlider);

        SetVolume(1f);
    }

    public void SetVolume(float volume)
    {
        var clamped = Math.Clamp(volume, 0f, 1f);
        _pendingVolume = clamped;
        _committedVolume = clamped;

        var percent = clamped * 100f;
        _volumeSlider.SetValueWithoutEvent(percent);
        UpdateLabel(percent);
    }

    private void OnSliderValueChanged(Range range)
    {
        // Dragging only previews the number in the window. The server gets one message on release.
        UpdateLabel(range.Value);
        _pendingVolume = Math.Clamp(range.Value / 100f, 0f, 1f);
    }

    private void OnSliderReleased(Slider slider)
    {
        if (MathF.Abs(_pendingVolume - _committedVolume) <= VolumeTolerance)
            return;

        // Commit once so changing volume does not spam network/audio state updates.
        _committedVolume = _pendingVolume;
        OnVolumeCommitted?.Invoke(_pendingVolume);
    }

    private void UpdateLabel(float percent)
    {
        _volumeLabel.Text = Loc.GetString("single-stream-audio-volume-window-volume",
            ("volume", (int) MathF.Round(percent)));
    }
}
