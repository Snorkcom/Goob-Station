// SPDX-FileCopyrightText: 2026 Corvax-Forge
//
// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Diagnostics.CodeAnalysis;
using System.Linq;
using Content.Server.Popups;
using Content.Shared.Access;
using Content.Shared.Access.Components;
using static Content.Shared.Access.Components.IdCardConsoleComponent;
using Content.Shared.Administration.Logs;
using Content.Shared.Database;
using Content.Shared.Roles;
using Content.Shared.StationRecords;
using Robust.Shared.Audio.Systems;
using Robust.Shared.Prototypes;

namespace Content.Server.Access.Systems;

public sealed partial class IdCardConsoleSystem // corvax goob edit - made partial
{
    private static readonly HashSet<ProtoId<AccessLevelPrototype>> ExtendedAccessExclusions =
    [
        "Armory",
        "Captain",
        "ChiefMedicalOfficer",
        "HeadOfPersonnel",
        "ResearchDirector",
        "HeadOfSecurity",
        "Quartermaster",
        "ChiefEngineer",
        "NanotrasenRepresentative",
        "BlueshieldOfficer",
        "CentralCommand",
    ];

    [Dependency] private readonly PopupSystem _popup = default!;
    [Dependency] private readonly SharedAudioSystem _audio = default!;

    /// <summary>
    /// Registers the CorvaxGoob bulk access message handler for the ID console.
    /// </summary>
    private void InitializeCorvaxGoobBulkAccess()
    {
        SubscribeLocalEvent<IdCardConsoleComponent, IdCardConsoleBulkAccessMessage>(OnBulkAccessMessage);
    }

    private void OnBulkAccessMessage(EntityUid uid, IdCardConsoleComponent component, IdCardConsoleBulkAccessMessage args)
    {
        if (args.Actor is not { Valid: true } player)
            return;

        // Balance hook: route this through a do-after if bulk access changes need a delay later.
        // var doAfter = new DoAfterArgs(EntityManager, player, TimeSpan.FromSeconds(3),
        //     new IdCardConsoleBulkAccessDoAfterEvent(args.Action), uid, target: component.TargetIdSlot.Item, used: uid)
        // {
        //     BreakOnMove = true,
        //     BreakOnDamage = true,
        // };
        // _doAfter.TryStartDoAfter(doAfter);
        // return;

        TryApplyBulkAccessAction(uid, args.Action, player, component);

        UpdateUserInterface(uid, component, args);
    }

    private void TryApplyBulkAccessAction(
        EntityUid uid,
        IdCardConsoleBulkAccessAction action,
        EntityUid player,
        IdCardConsoleComponent? component = null)
    {
        if (!Resolve(uid, ref component))
            return;

        if (component.PrivilegedIdSlot.Item is not { Valid: true } privilegedId
            || component.TargetIdSlot.Item is not { Valid: true } targetId
            || !PrivilegedIdIsAuthorized(uid, component)
            || !TryComp<IdCardComponent>(targetId, out var targetIdComponent))
        {
            return;
        }

        var privilegedTags = _accessReader.FindAccessTags(privilegedId).ToHashSet();
        var visibleTags = component.AccessLevels.ToHashSet();
        // Bulk changes stay inside the console-visible surface so hidden tags cannot be copied or cleared accidentally.
        var modifiableTags = privilegedTags.Intersect(visibleTags).ToHashSet();
        var oldTags = (_access.TryGetTags(targetId) ?? Array.Empty<ProtoId<AccessLevelPrototype>>()).ToHashSet();
        HashSet<ProtoId<AccessLevelPrototype>> newTags;
        JobPrototype? newJob = null;
        var newJobTitle = targetIdComponent.LocalizedJobTitle ?? string.Empty;
        var changedIdentity = false;

        switch (action)
        {
            case IdCardConsoleBulkAccessAction.StandardAccess:
                if (!TryResolveJobFromTitle(targetIdComponent.LocalizedJobTitle, out var resetJob))
                {
                    ShowResetFailed(uid, player, component);
                    return;
                }

                newJob = resetJob;
                newJobTitle = resetJob.LocalizedName;
                var resetJobAccess = GetJobAccessTags(resetJob).Intersect(modifiableTags);
                newTags = oldTags.Except(modifiableTags).Union(resetJobAccess).ToHashSet();
                changedIdentity = ApplyJobIdentity(targetId, targetIdComponent, resetJob, resetJob.LocalizedName, player);
                break;
            case IdCardConsoleBulkAccessAction.Extended:
                newTags = oldTags.Union(GetExtendedAccessTags(modifiableTags)).ToHashSet();
                changedIdentity = TryAddAccessMarkerToJobTitle(targetId, targetIdComponent, player);
                break;
            case IdCardConsoleBulkAccessAction.Full:
                newTags = oldTags.Union(modifiableTags).ToHashSet();
                changedIdentity = TryAddAccessMarkerToJobTitle(targetId, targetIdComponent, player);
                break;
            default:
                return;
        }

        if (oldTags.SetEquals(newTags) && !changedIdentity)
            return;

        var changedAccess = !oldTags.SetEquals(newTags);
        if (changedAccess)
            _access.TrySetTags(targetId, newTags);

        if (newJob != null)
            UpdateStationRecord(targetId, targetIdComponent.FullName ?? string.Empty, newJobTitle, newJob);
        else if (changedIdentity)
            UpdateStationRecordJobTitle(targetId, targetIdComponent.LocalizedJobTitle ?? string.Empty);

        var addedTags = newTags.Except(oldTags).Select(tag => "+" + tag).ToList();
        var removedTags = oldTags.Except(newTags).Select(tag => "-" + tag).ToList();
        if (changedAccess)
        {
            _adminLogger.Add(LogType.Action, LogImpact.Medium,
                $"{ToPrettyString(player):player} has bulk-modified {ToPrettyString(targetId):entity} with the following accesses: [{string.Join(", ", addedTags.Union(removedTags))}] [{string.Join(", ", newTags)}]");
        }

        _audio.PlayPvs(component.BulkAccessSuccessSound, uid);
    }

    private static HashSet<ProtoId<AccessLevelPrototype>> GetExtendedAccessTags(HashSet<ProtoId<AccessLevelPrototype>> privilegedTags)
    {
        return privilegedTags.Except(ExtendedAccessExclusions).ToHashSet();
    }

    private HashSet<ProtoId<AccessLevelPrototype>> GetJobAccessTags(JobPrototype job)
    {
        var tags = job.Access.ToHashSet();

        foreach (var group in job.AccessGroups)
        {
            if (!_prototype.TryIndex(group, out AccessGroupPrototype? groupPrototype))
                continue;

            tags.UnionWith(groupPrototype.Tags);
        }

        return tags;
    }

    private bool TryAddAccessMarkerToJobTitle(EntityUid targetId, IdCardComponent targetIdComponent, EntityUid player)
    {
        var jobTitle = targetIdComponent.LocalizedJobTitle ?? string.Empty;
        // The marker is only a visual flag, so repeated bulk grants should not stack extra plus signs.
        if (jobTitle.TrimEnd().EndsWith('+'))
            return false;

        var markedJobTitle = string.IsNullOrWhiteSpace(jobTitle)
            ? "+"
            : $"{jobTitle}+";

        return _idCard.TryChangeJobTitle(targetId, markedJobTitle, targetIdComponent, player: player);
    }

    private bool ApplyJobIdentity(
        EntityUid targetId,
        IdCardComponent targetIdComponent,
        JobPrototype job,
        string jobTitle,
        EntityUid player)
    {
        var changed = false;

        if (!string.Equals(targetIdComponent.LocalizedJobTitle, jobTitle, StringComparison.CurrentCulture))
        {
            _idCard.TryChangeJobTitle(targetId, jobTitle, targetIdComponent, player: player);
            changed = true;
        }

        if (_prototype.TryIndex(job.Icon, out var jobIcon))
        {
            if (targetIdComponent.JobIcon != jobIcon.ID)
            {
                _idCard.TryChangeJobIcon(targetId, jobIcon, targetIdComponent, player: player);
                changed = true;
            }

            var departments = _prototype
                .EnumeratePrototypes<DepartmentPrototype>()
                .Where(department => department.Roles.Contains(job.ID))
                .Select(department => new ProtoId<DepartmentPrototype>(department.ID))
                .ToHashSet();

            if (!targetIdComponent.JobDepartments.ToHashSet().SetEquals(departments))
            {
                _idCard.TryChangeJobDepartment(targetId, job, targetIdComponent);
                changed = true;
            }
        }

        if (targetIdComponent.JobPrototype != job.ID)
        {
            targetIdComponent.JobPrototype = job.ID;
            Dirty(targetId, targetIdComponent);
            changed = true;
        }

        return changed;
    }

    private bool TryResolveJobFromTitle(string? jobTitle, [NotNullWhen(true)] out JobPrototype? job)
    {
        job = null;
        var normalizedTitle = NormalizeJobTitle(jobTitle);
        if (string.IsNullOrWhiteSpace(normalizedTitle))
            return false;

        var jobs = _prototype
            .EnumeratePrototypes<JobPrototype>()
            .Where(x => x.OverrideConsoleVisibility.GetValueOrDefault(x.SetPreference))
            .ToList();

        // Standard access recovery uses the localized card title, first exactly and then by one unambiguous contained job name.
        var exactMatches = jobs
            .Where(x => string.Equals(NormalizeJobTitle(x.LocalizedName), normalizedTitle, StringComparison.CurrentCultureIgnoreCase))
            .ToList();

        if (exactMatches.Count == 1)
        {
            job = exactMatches[0];
            return true;
        }

        if (exactMatches.Count > 1)
            return false;

        var containedMatches = jobs
            .Where(x =>
            {
                var localizedName = NormalizeJobTitle(x.LocalizedName);
                return !string.IsNullOrWhiteSpace(localizedName)
                    && normalizedTitle.Contains(localizedName, StringComparison.CurrentCultureIgnoreCase);
            })
            .ToList();

        if (containedMatches.Count != 1)
            return false;

        job = containedMatches[0];
        return true;
    }

    private static string NormalizeJobTitle(string? jobTitle)
    {
        var normalized = jobTitle?.Trim() ?? string.Empty;
        if (normalized.EndsWith('+'))
            normalized = normalized[..^1].Trim();

        return normalized;
    }

    private void ShowResetFailed(EntityUid uid, EntityUid player, IdCardConsoleComponent component)
    {
        _popup.PopupEntity(Loc.GetString("id-card-console-reset-job-failed"), uid, player);
        _audio.PlayPvs(component.BulkAccessFailureSound, uid);
    }

    private void UpdateStationRecordJobTitle(EntityUid targetId, string newJobTitle)
    {
        if (!TryComp<StationRecordKeyStorageComponent>(targetId, out var keyStorage)
            || keyStorage.Key is not { } key
            || !_record.TryGetRecord<GeneralStationRecord>(key, out var record))
        {
            return;
        }

        record.JobTitle = newJobTitle;
        _record.Synchronize(key);
    }
}
