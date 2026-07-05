using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.Audio;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class SingleStreamAudioVolumeComponent : Component
{
    /// <summary>
    /// Slider value from 0 to 1. The system converts it into a bounded dB volume.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Volume = 1f;

    /// <summary>
    /// Current audio entity controlled by this component.
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? AudioStream;

    /// <summary>
    /// The stream's original dB volume before the user slider is applied.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float BaseVolume;

    /// <summary>
    /// Keeps the stream alive while silencing it for powered-off or inactive devices.
    /// </summary>
    [DataField, AutoNetworkedField]
    public bool Muted;

    /// <summary>
    /// If true, the volume verb and stale messages are blocked while the entity is unpowered.
    /// </summary>
    [DataField]
    public bool RequiresPower = true;
}
