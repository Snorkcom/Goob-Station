// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Shared.Containers.ItemSlots;
using Content.Shared.Roles;
using Robust.Shared.Audio;
using Robust.Shared.GameStates;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxGoob.CardPdaPainter;

[RegisterComponent]
public sealed partial class CardPdaPainterComponent : Component
{
    public const string TargetSlotId = "CardPdaPainter-target";

    [DataField]
    public ItemSlot TargetSlot = new();

    [DataField]
    public SoundSpecifier PaintSound = new SoundPathSpecifier("/Audio/Effects/spray2.ogg");
}

[RegisterComponent, NetworkedComponent, AutoGenerateComponentState(true)]
public sealed partial class CardPdaVisualOverrideComponent : Component
{
    [DataField, AutoNetworkedField]
    public EntProtoId? VisualPrototype;
}

[Serializable, NetSerializable]
public enum CardPdaPainterUiKey : byte
{
    Key
}

[Serializable, NetSerializable]
public enum CardPdaPainterTargetType : byte
{
    None,
    IdCard,
    Pda
}

[Serializable, NetSerializable]
public readonly record struct CardPdaPainterJobEntry(
    ProtoId<JobPrototype> JobId,
    EntProtoId VisualPrototype);

[Serializable, NetSerializable]
public sealed class CardPdaPainterBoundUserInterfaceState : BoundUserInterfaceState
{
    public readonly bool IsTargetPresent;
    public readonly string TargetName;
    public readonly CardPdaPainterTargetType TargetType;
    public readonly List<CardPdaPainterJobEntry> Jobs;

    public CardPdaPainterBoundUserInterfaceState(
        bool isTargetPresent,
        string targetName,
        CardPdaPainterTargetType targetType,
        List<CardPdaPainterJobEntry> jobs)
    {
        IsTargetPresent = isTargetPresent;
        TargetName = targetName;
        TargetType = targetType;
        Jobs = jobs;
    }
}

[Serializable, NetSerializable]
public sealed class CardPdaPainterRepaintMessage : BoundUserInterfaceMessage
{
    public readonly ProtoId<JobPrototype> JobId;

    public CardPdaPainterRepaintMessage(ProtoId<JobPrototype> jobId)
    {
        JobId = jobId;
    }
}
