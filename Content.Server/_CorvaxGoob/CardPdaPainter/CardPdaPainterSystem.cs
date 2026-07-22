// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Popups;
using Content.Shared._CorvaxGoob.CardPdaPainter;
using Content.Shared.Access.Components;
using Content.Shared.Clothing.Components;
using Content.Shared.Clothing.EntitySystems;
using Content.Shared.Item;
using Content.Shared.PDA;
using Content.Shared.Roles;
using Robust.Server.Audio;
using Robust.Server.GameObjects;
using Robust.Shared.Containers;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxGoob.CardPdaPainter;

public sealed class CardPdaPainterSystem : SharedCardPdaPainterSystem
{
    private static readonly Dictionary<string, EntProtoId> JobPdaFallbacks = new()
    {
        { "AtmosphericTechnician", "AtmosPDA" },
        { "Brigmedic", "BrigmedicPDA" },
        { "CargoTechnician", "CargoPDA" },
        { "ChiefEngineer", "CEPDA" },
        { "ChiefMedicalOfficer", "CMOPDA" },
        { "HeadOfPersonnel", "HoPPDA" },
        { "HeadOfSecurity", "HoSPDA" },
        { "MedicalDoctor", "MedicalPDA" },
        { "RadioHost", "RadioHostPDA" },
        { "ResearchDirector", "RnDPDA" },
        { "Roboticist", "RoboticistPDA" },
        { "SalvageSpecialist", "SalvagePDA" },
        { "Scientist", "SciencePDA" },
        { "SecurityOfficer", "SecurityPDA" },
        { "StationEngineer", "EngineerPDA" },
        { "Virologist", "VirologistPDA" },
    };

    [Dependency] private readonly IPrototypeManager _prototype = default!;
    [Dependency] private readonly SharedItemSystem _item = default!;
    [Dependency] private readonly ClothingSystem _clothing = default!;
    [Dependency] private readonly SharedAppearanceSystem _appearance = default!;
    [Dependency] private readonly UserInterfaceSystem _ui = default!;
    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly AudioSystem _audio = default!;

    public override void Initialize()
    {
        base.Initialize();

        SubscribeLocalEvent<CardPdaPainterComponent, BoundUIOpenedEvent>(OnUiOpened);
        SubscribeLocalEvent<CardPdaPainterComponent, EntInsertedIntoContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<CardPdaPainterComponent, EntRemovedFromContainerMessage>(OnSlotChanged);
        SubscribeLocalEvent<CardPdaPainterComponent, CardPdaPainterRepaintMessage>(OnRepaint);
    }

    private void OnUiOpened(Entity<CardPdaPainterComponent> ent, ref BoundUIOpenedEvent args)
    {
        UpdateUi(ent);
    }

    private void OnSlotChanged(Entity<CardPdaPainterComponent> ent, ref EntInsertedIntoContainerMessage args)
    {
        if (args.Container.ID == CardPdaPainterComponent.TargetSlotId)
            UpdateUi(ent);
    }

    private void OnSlotChanged(Entity<CardPdaPainterComponent> ent, ref EntRemovedFromContainerMessage args)
    {
        if (args.Container.ID == CardPdaPainterComponent.TargetSlotId)
            UpdateUi(ent);
    }

    private void OnRepaint(Entity<CardPdaPainterComponent> ent, ref CardPdaPainterRepaintMessage args)
    {
        if (ent.Comp.TargetSlot.Item is not { } target)
            return;

        var targetType = GetTargetType(target);
        if (targetType == CardPdaPainterTargetType.None)
            return;

        if (!TryGetVisualPrototype(args.JobId, targetType, out var visualPrototype))
            return;

        if (!ApplyVisualTemplate(target, visualPrototype))
            return;

        _audio.PlayPvs(ent.Comp.PaintSound, ent);
        _popup.PopupEntity(Loc.GetString("card-pda-painter-popup-success"), ent, args.Actor);
        UpdateUi(ent);
    }

    private void UpdateUi(Entity<CardPdaPainterComponent> ent)
    {
        var target = ent.Comp.TargetSlot.Item;
        var targetType = target is { } targetUid
            ? GetTargetType(targetUid)
            : CardPdaPainterTargetType.None;

        var jobs = targetType == CardPdaPainterTargetType.None
            ? new List<CardPdaPainterJobEntry>()
            : GetJobEntries(targetType);

        var state = new CardPdaPainterBoundUserInterfaceState(
            target != null,
            target != null ? Name(target.Value) : string.Empty,
            targetType,
            jobs);

        _ui.SetUiState(ent.Owner, CardPdaPainterUiKey.Key, state);
    }

    private List<CardPdaPainterJobEntry> GetJobEntries(CardPdaPainterTargetType targetType)
    {
        var entries = new List<CardPdaPainterJobEntry>();

        foreach (var job in _prototype.EnumeratePrototypes<JobPrototype>())
        {
            if (!job.OverrideConsoleVisibility.GetValueOrDefault(job.SetPreference))
                continue;

            if (TryGetVisualPrototype(job.ID, targetType, out var visualPrototype))
                entries.Add(new CardPdaPainterJobEntry(job.ID, visualPrototype));
        }

        entries.Sort((x, y) =>
        {
            var xName = _prototype.Index(x.JobId).LocalizedName;
            var yName = _prototype.Index(y.JobId).LocalizedName;
            return string.Compare(xName, yName, StringComparison.CurrentCulture);
        });

        return entries;
    }

    private bool TryGetVisualPrototype(
        ProtoId<JobPrototype> jobId,
        CardPdaPainterTargetType targetType,
        out EntProtoId visualPrototype)
    {
        visualPrototype = default;

        if (!_prototype.TryIndex(jobId, out JobPrototype? job) ||
            job.StartingGear is not { } startingGearId ||
            !_prototype.TryIndex(startingGearId, out StartingGearPrototype? startingGear))
        {
            return false;
        }

        if (targetType == CardPdaPainterTargetType.Pda)
            return TryGetPdaVisualPrototype(job, startingGear, out visualPrototype);

        if (startingGear.Equipment.TryGetValue("id", out var idGear) &&
            TryGetIdCardVisualPrototype(idGear, out visualPrototype))
        {
            return true;
        }

        if (TryGetPdaVisualPrototype(job, startingGear, out var pdaPrototype) &&
            TryGetIdCardVisualPrototype(pdaPrototype, out visualPrototype))
        {
            return true;
        }

        var jobIdCard = $"{job.ID}IDCard";
        return TryGetIdCardVisualPrototype(jobIdCard, out visualPrototype);
    }

    private bool TryGetPdaVisualPrototype(
        JobPrototype job,
        StartingGearPrototype startingGear,
        out EntProtoId visualPrototype)
    {
        visualPrototype = default;

        if (startingGear.Equipment.TryGetValue("id", out var idGear) &&
            TryGetPdaVisualPrototype(idGear, out visualPrototype))
        {
            return true;
        }

        var jobPda = $"{job.ID}PDA";
        if (TryGetPdaVisualPrototype(jobPda, out visualPrototype))
            return true;

        return JobPdaFallbacks.TryGetValue(job.ID, out var fallback) &&
               TryGetPdaVisualPrototype(fallback, out visualPrototype);
    }

    private bool TryGetPdaVisualPrototype(EntProtoId prototypeId, out EntProtoId visualPrototype)
    {
        visualPrototype = default;

        if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out var prototype) ||
            !prototype.TryGetComponent<PdaComponent>(out _, Factory))
        {
            return false;
        }

        visualPrototype = prototypeId;
        return true;
    }

    private bool TryGetIdCardVisualPrototype(EntProtoId prototypeId, out EntProtoId visualPrototype)
    {
        visualPrototype = default;

        if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out var prototype))
            return false;

        if (prototype.TryGetComponent<PdaComponent>(out var pda, Factory))
        {
            if (string.IsNullOrEmpty(pda.IdCard))
                return false;

            prototypeId = pda.IdCard;
        }

        if (!_prototype.TryIndex<EntityPrototype>(prototypeId, out var idCardPrototype) ||
            !idCardPrototype.TryGetComponent<IdCardComponent>(out _, Factory))
        {
            return false;
        }

        visualPrototype = prototypeId;
        return true;
    }

    private CardPdaPainterTargetType GetTargetType(EntityUid target)
    {
        if (HasComp<PdaComponent>(target))
            return CardPdaPainterTargetType.Pda;

        if (HasComp<IdCardComponent>(target))
            return CardPdaPainterTargetType.IdCard;

        return CardPdaPainterTargetType.None;
    }

    private bool ApplyVisualTemplate(EntityUid target, EntProtoId visualPrototype)
    {
        if (!_prototype.TryIndex<EntityPrototype>(visualPrototype, out var prototype))
            return false;

        var visualOverride = EnsureComp<CardPdaVisualOverrideComponent>(target);
        visualOverride.VisualPrototype = visualPrototype;
        Dirty(target, visualOverride);

        if (TryComp<ItemComponent>(target, out var item) &&
            prototype.TryGetComponent<ItemComponent>(out var otherItem, Factory))
        {
            _item.CopyVisuals(target, otherItem, item);
        }

        if (TryComp<ClothingComponent>(target, out var clothing) &&
            prototype.TryGetComponent<ClothingComponent>(out var otherClothing, Factory))
        {
            _clothing.CopyVisuals(target, otherClothing, clothing);
        }

        if (TryComp<AppearanceComponent>(target, out var appearance) &&
            prototype.TryGetComponent<AppearanceComponent>(out var otherAppearance, Factory))
        {
            _appearance.AppendData(otherAppearance, target);
            Dirty(target, appearance);
        }

        return true;
    }
}
