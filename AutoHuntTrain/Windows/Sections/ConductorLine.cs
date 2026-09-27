using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Tasks;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;

namespace AutoHuntTrain.Windows.Sections;

// Who the ride follows and where that came from; rebuilt only when the conductor, its source or the language changes,
// so drawing it every frame allocates nothing.
internal static class ConductorLine
{
    private static CachedText text;

    public static string Get(RideProgress progress)
    {
        var identity = progress.Conductor;
        var key = HashCode.Combine((int)progress.ConductorSource, identity.WorldId, identity.Name, progress.StartTerritoryId);
        if (text.TryGet(key, out var line))
        {
            return line;
        }

        return text.Set(key, Build(progress, identity));
    }

    private static string Build(RideProgress progress, in ConductorIdentity identity)
    {
        var name = Conductor.Describe(identity);
        return progress.ConductorSource switch
        {
            ConductorSource.Announced => Loc.T(L.Feed.ConductorAnnounced, name),
            ConductorSource.Picked => Loc.T(L.Feed.ConductorPicked, name),
            ConductorSource.Manual => Loc.T(L.Feed.ConductorNamed, name),
            _ => progress.StartTerritoryId != 0
                ? Loc.T(L.Feed.ConductorPendingIn, TerritoryNames.Of(progress.StartTerritoryId))
                : Loc.T(L.Feed.ConductorPending),
        };
    }
}
