using Robust.Shared.Audio;
using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.StationRadio.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class VinylPlayerComponent : Component
{
    /// <summary>
    /// Should the vinyl player relay to radios around the station, should only be true for the radiostation vinyl player
    /// </summary>
    [DataField]
    public bool RelayToRadios;

    /// <summary>
    /// The sound entity being played
    /// </summary>
    [DataField, AutoNetworkedField]
    public EntityUid? SoundEntity;

    /// <summary>
    /// Default local audio params for the record player.
    /// </summary>
    [DataField, AutoNetworkedField]
    public AudioParams AudioParams = AudioParams.Default.WithVolume(3f).WithMaxDistance(4.5f);

    /// <summary>
    /// Music volume multiplier controlled from the radio volume UI.
    /// </summary>
    [DataField, AutoNetworkedField]
    public float Volume = 1f;
}
