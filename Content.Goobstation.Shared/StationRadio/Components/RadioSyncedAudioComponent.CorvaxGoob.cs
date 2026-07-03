using Robust.Shared.GameStates;

namespace Content.Goobstation.Shared.StationRadio.Components;

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState]
public sealed partial class RadioSyncedAudioComponent : Component
{
    /// <summary>
    /// Original broadcast clock metadata. Manual client resync uses AudioComponent.AudioStart as the authoritative clock.
    /// </summary>
    [DataField, AutoNetworkedField]
    public TimeSpan BroadcastStartTime;
}
