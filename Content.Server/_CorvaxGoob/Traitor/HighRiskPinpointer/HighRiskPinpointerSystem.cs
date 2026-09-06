// SPDX-License-Identifier: AGPL-3.0-or-later

using Content.Server.Mind;
using Content.Server.Pinpointer;
using Content.Server.Popups;
using Content.Server.Roles;
using Content.Shared._CorvaxGoob.Traitor.HighRiskPinpointer;
using Content.Shared.Forensics.Components;
using Content.Shared.Interaction;
using Content.Shared.Pinpointer;
using Content.Shared.Roles.Components;
using Content.Shared.UserInterface;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxGoob.Traitor.HighRiskPinpointer;

/// <summary>
/// Lets traitors configure a purchased pinpointer to track high-risk steal targets or an exact DNA sequence.
/// </summary>
public sealed partial class HighRiskPinpointerSystem : EntitySystem
{
    [Dependency] private MindSystem _mind = default!;
    [Dependency] private PinpointerSystem _pinpointer = default!;
    [Dependency] private PopupSystem _popup = default!;
    [Dependency] private IPrototypeManager _prototypes = default!;
    [Dependency] private RoleSystem _roles = default!;

    public override void Initialize()
    {
        SubscribeLocalEvent<HighRiskPinpointerComponent, ActivateInWorldEvent>(
            OnActivate,
            after: [typeof(PinpointerSystem)],
            before: [typeof(ActivatableUISystem)]);
        SubscribeLocalEvent<HighRiskPinpointerComponent, ActivatableUIOpenAttemptEvent>(OnUiOpenAttempt);
        SubscribeLocalEvent<HighRiskPinpointerComponent, HighRiskPinpointerSearchMessage>(OnSearchRequested);
    }

    private void OnActivate(Entity<HighRiskPinpointerComponent> ent, ref ActivateInWorldEvent args)
    {
        // The ordinary pinpointer consumes E activation even when toggling is disabled; pass it on to the UI instead.
        args.Handled = false;
    }

    private void OnUiOpenAttempt(Entity<HighRiskPinpointerComponent> ent, ref ActivatableUIOpenAttemptEvent args)
    {
        // Reject access before the server opens the interface for a non-traitor.
        if (IsTraitor(args.User))
            return;

        args.Cancel();
    }

    private void OnSearchRequested(Entity<HighRiskPinpointerComponent> ent, ref HighRiskPinpointerSearchMessage args)
    {
        // BUI messages are validated again because a client can send them without legitimately opening the window.
        if (!IsTraitor(args.Actor))
        {
            if (TryComp<PinpointerComponent>(ent, out var pinpointer) && pinpointer.IsActive)
                _pinpointer.TogglePinpointer(ent.Owner, pinpointer);

            _popup.PopupEntity(Loc.GetString("high-risk-pinpointer-access-denied"), ent, args.Actor);
            return;
        }

        List<EntityUid> matches;
        string failureMessage;
        string trackedTarget;

        if (args.SelectionId == HighRiskPinpointerTargetCatalog.Targets.Length)
        {
            if (string.IsNullOrWhiteSpace(args.Dna))
                return;

            matches = FindEntitiesByDna(args.Dna);
            failureMessage = "high-risk-pinpointer-dna-not-found";
            trackedTarget = Loc.GetString("high-risk-pinpointer-dna-target", ("dna", args.Dna.ToUpperInvariant()));
        }
        else
        {
            if (args.SelectionId < 0 || args.SelectionId >= HighRiskPinpointerTargetCatalog.Targets.Length)
                return;

            var target = HighRiskPinpointerTargetCatalog.Targets[args.SelectionId];
            matches = FindEntitiesByPrototype(target.Prototypes);
            failureMessage = "high-risk-pinpointer-target-not-found";
            trackedTarget = target.Name is { } nameId
                ? Loc.GetString(nameId)
                : _prototypes.Index<EntityPrototype>(target.Prototypes[0]).Name;
        }

        if (matches.Count == 0)
        {
            _popup.PopupEntity(Loc.GetString(failureMessage), ent, args.Actor);
            return;
        }

        if (TrackTargets(ent, matches))
        {
            _popup.PopupEntity(
                Loc.GetString("high-risk-pinpointer-search-started", ("target", trackedTarget)),
                ent,
                args.Actor);
        }
    }

    private bool IsTraitor(EntityUid user)
    {
        return _mind.TryGetMind(user, out var mindId, out _) && _roles.MindHasRole<TraitorRoleComponent>(mindId);
    }

    // Give every matching entity to the standard pinpointer, which displays the direction to the nearest one.
    private bool TrackTargets(Entity<HighRiskPinpointerComponent> ent, List<EntityUid> targets)
    {
        if (!TryComp<PinpointerComponent>(ent, out var pinpointer))
            return false;

        _pinpointer.SetTargets(ent.Owner, targets, pinpointer);

        if (!pinpointer.IsActive)
            _pinpointer.TogglePinpointer(ent.Owner, pinpointer);

        return true;
    }

    private List<EntityUid> FindEntitiesByDna(string dna)
    {
        var matches = new List<EntityUid>();
        var query = EntityQueryEnumerator<DnaComponent>();

        while (query.MoveNext(out var uid, out var targetDna))
        {
            // Generated DNA is uppercase, but players may enter the same sequence in either case.
            if (string.Equals(targetDna.DNA, dna, StringComparison.OrdinalIgnoreCase))
                matches.Add(uid);
        }

        return matches;
    }
}
