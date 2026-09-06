// SPDX-License-Identifier: AGPL-3.0-or-later

using Robust.Shared.GameStates;
using Robust.Shared.Localization;
using Robust.Shared.Prototypes;
using Robust.Shared.Serialization;

namespace Content.Shared._CorvaxGoob.Traitor.HighRiskPinpointer;

/// <summary>
/// Marks a Syndicate pinpointer that can select station high-risk targets or a target by DNA.
/// </summary>
[RegisterComponent, NetworkedComponent]
public sealed partial class HighRiskPinpointerComponent : Component;

[Serializable, NetSerializable]
public enum HighRiskPinpointerUiKey : byte
{
    Key
}

/// <summary>
/// Defines the fixed targets available in the interface and the entity prototypes matched by each target.
/// </summary>
public static class HighRiskPinpointerTargetCatalog
{
    // The UI displays this manually maintained order directly instead of sorting targets at runtime.
    public static readonly HighRiskPinpointerTargetDefinition[] Targets =
    [
        new(["WeaponAntiqueLaser"]),
        new(["Hypospray"]),
        new(["ClothingHandsKnuckleDustersQM"]),
        new(["JetpackCaptainFilled"]),
        new(["ExecutiveBriefcaseEmpty"]),
        new(["NukeDisk"]),
        new(["MobCorgiIan", "MobCorgiIanOld", "MobCorgiLisa", "MobCorgiIanPup"], "high-risk-pinpointer-target-hop-corgi"),
        new(["ClothingBeltGeminiHoloProjector"]),
        new(["FoodMeatCorgi"]),
        new(["ClothingShoesBootsMagAdv"]),
        new(["HandTeleporter"]),
        new(["RapidSyringeGun"]),
        new(["BoxFolderQmClipboard"]),
        new(["ClothingOuterHardsuitRd"]),
        new(["WeaponEnergyShotgun"]),
        new(["WeaponEnergyMagnum"]),
        new(["WeaponEnergyGunLawbringer"]),
        new(["Justice"]),
        new(["CaptainIDCard"])
    ];
}

/// <summary>
/// Requests tracking for either a catalog entry or the DNA option placed immediately after the catalog.
/// </summary>
[Serializable, NetSerializable]
public sealed class HighRiskPinpointerSearchMessage(int selectionId, string dna) : BoundUserInterfaceMessage
{
    public readonly int SelectionId = selectionId;
    public readonly string Dna = dna;
}

/// <summary>
/// Groups entity prototypes shown as one target option. An optional name overrides the first prototype's display name.
/// </summary>
public sealed record HighRiskPinpointerTargetDefinition(EntProtoId[] Prototypes, LocId? Name = null);
