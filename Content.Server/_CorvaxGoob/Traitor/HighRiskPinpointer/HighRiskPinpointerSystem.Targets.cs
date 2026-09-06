// SPDX-License-Identifier: AGPL-3.0-or-later

using System.Linq;
using Robust.Shared.Prototypes;

namespace Content.Server._CorvaxGoob.Traitor.HighRiskPinpointer;

/// <summary>
/// Resolves spawned entities that match a selected high-risk target.
/// </summary>
public sealed partial class HighRiskPinpointerSystem
{
    // Return every spawned match so the pinpointer can dynamically follow whichever one is nearest.
    private List<EntityUid> FindEntitiesByPrototype(IReadOnlyCollection<EntProtoId> prototypes)
    {
        var matches = new List<EntityUid>();
        var query = EntityQueryEnumerator<MetaDataComponent>();

        while (query.MoveNext(out var uid, out var metadata))
        {
            if (metadata.EntityPrototype is { } prototype && prototypes.Any(id => id.Id == prototype.ID))
                matches.Add(uid);
        }

        return matches;
    }
}
