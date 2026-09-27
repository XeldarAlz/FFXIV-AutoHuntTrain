using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Travel;

namespace AutoHuntTrain.Windows;

internal static class RegionLabels
{
    public static string Name(WorldRegion region) => region switch
    {
        WorldRegion.Japan => Loc.T(L.Feed.RegionJapan),
        WorldRegion.NorthAmerica => Loc.T(L.Feed.RegionNorthAmerica),
        WorldRegion.Europe => Loc.T(L.Feed.RegionEurope),
        WorldRegion.Oceania => Loc.T(L.Feed.RegionOceania),
        _ => Loc.T(L.Feed.RegionUnknown),
    };
}
