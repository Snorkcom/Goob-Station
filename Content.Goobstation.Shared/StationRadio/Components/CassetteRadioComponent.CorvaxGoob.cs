using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.StationRadio.Components;

[RegisterComponent]
public sealed partial class CassetteRadioComponent : Component
{
    [DataField]
    public bool Active;

    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "RadioShow";

    [DataField]
    public AudioParams AudioParams = AudioParams.Default.WithVolume(0f);

    [DataField]
    public float Volume = 1f;

    public EntityUid? SoundEntity;

    public ICommonSession? SoundSession;

    public EntityUid? Wearer;
}
