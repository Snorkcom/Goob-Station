using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.StationRadio;

[Serializable, NetSerializable]
public enum StationRadioVolumeUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class StationRadioVolumeState(float volume) : BoundUserInterfaceState
{
    public readonly float Volume = volume;
}

[Serializable, NetSerializable]
public sealed class StationRadioSetVolumeMessage(float volume) : BoundUserInterfaceMessage
{
    public readonly float Volume = volume;
}
