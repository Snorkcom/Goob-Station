using Robust.Shared.Serialization;

namespace Content.Goobstation.Shared.Audio;

[Serializable, NetSerializable]
public enum SingleStreamAudioVolumeUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public sealed class SingleStreamAudioVolumeState(float volume) : BoundUserInterfaceState
{
    public readonly float Volume = volume;
}

[Serializable, NetSerializable]
public sealed class SingleStreamAudioVolumeSetMessage(float volume) : BoundUserInterfaceMessage
{
    public readonly float Volume = volume;
}
