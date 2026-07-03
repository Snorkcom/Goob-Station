using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.StationRadio;

[Serializable, NetSerializable]
public enum StationRadioVolumeUiKey : byte
{
    Key
}

/// <summary>
/// Sends the current per-device music volume to the volume window.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationRadioVolumeState(float volume) : BoundUserInterfaceState
{
    public readonly float Volume = volume;
}

/// <summary>
/// Commits the volume chosen by the user after the slider is released.
/// </summary>
[Serializable, NetSerializable]
public sealed class StationRadioSetVolumeMessage(float volume) : BoundUserInterfaceMessage
{
    public readonly float Volume = volume;
}
