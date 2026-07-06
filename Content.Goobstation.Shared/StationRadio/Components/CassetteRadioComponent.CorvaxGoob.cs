using Content.Shared.Radio;
using Robust.Shared.Audio;
using Robust.Shared.Player;
using Robust.Shared.Prototypes;

namespace Content.Goobstation.Shared.StationRadio.Components;

[RegisterComponent]
public sealed partial class CassetteRadioComponent : Component
{
    /// <summary>
    /// Whether the built-in personal radio receiver is enabled.
    /// </summary>
    [DataField]
    public bool Active;

    /// <summary>
    /// Radio channel used for the DJ speech routed to the wearer only.
    /// </summary>
    [DataField]
    public ProtoId<RadioChannelPrototype> Channel = "RadioShow";

    /// <summary>
    /// Base params for the private music stream played only to the wearer.
    /// </summary>
    [DataField]
    public AudioParams AudioParams = AudioParams.Default.WithVolume(0f);

    /// <summary>
    /// User-controlled music volume multiplier for the private stream.
    /// </summary>
    [DataField]
    public float Volume = 1f;

    /// <summary>
    /// Current private music stream entity, if one is playing.
    /// </summary>
    public EntityUid? SoundEntity;

    /// <summary>
    /// Session that owns the private stream; used to recreate audio after reconnects.
    /// </summary>
    public ICommonSession? SoundSession;

    /// <summary>
    /// Player currently allowed to hear radio text and private music from this cassette.
    /// </summary>
    public EntityUid? Wearer;
}
