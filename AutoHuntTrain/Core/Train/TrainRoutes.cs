using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Marks;
using Dalamud.Game;
using ECommons.DalamudServices;
using Lumina.Excel;
using Lumina.Excel.Sheets;

namespace AutoHuntTrain.Core.Train;

// The order a train of one expansion visits its zones, taken cyclically from the zone it started in.
internal readonly record struct TrainRoute(ExpansionKind Expansion, uint[] Zones, int StartIndex)
{
    public int Length => Zones.Length;

    public double MinutesPerZone => TrainRoutes.MinutesPerZone(Expansion);

    public uint ZoneAt(int step) => Zones[(StartIndex + step) % Zones.Length];

    public bool Contains(uint territoryId) => Array.IndexOf(Zones, territoryId) >= 0;
}

// The standard zone order of a hunt train per expansion, as territory ids. Every listed id is checked against its
// English place name once, and one that no longer matches is left out rather than sending a ride to the wrong zone.
internal static class TrainRoutes
{
    // A typical conductor pace: two A ranks a zone from Heavensward on, one a zone in A Realm Reborn.
    private const double MinutesPerZoneTwoARanks = 3.0;
    private const double MinutesPerZoneRealmReborn = 1.5;
    private const uint OpenWorldIntendedUse = 1;
    private const uint RealmRebornExVersion = 0;
    private const int ExpansionCount = 6;

    private static readonly RouteStop[] heavenswardStops =
    [
        new(397, "Coerthas Western Highlands"),
        new(401, "The Sea of Clouds"),
        new(402, "Azys Lla"),
        new(398, "The Dravanian Forelands"),
        new(399, "The Dravanian Hinterlands"),
        new(400, "The Churning Mists"),
    ];

    private static readonly RouteStop[] stormbloodStops =
    [
        new(612, "The Fringes"),
        new(620, "The Peaks"),
        new(621, "The Lochs"),
        new(613, "The Ruby Sea"),
        new(614, "Yanxia"),
        new(622, "The Azim Steppe"),
    ];

    private static readonly RouteStop[] shadowbringersStops =
    [
        new(813, "Lakeland"),
        new(814, "Kholusia"),
        new(815, "Amh Araeng"),
        new(816, "Il Mheg"),
        new(817, "The Rak'tika Greatwood"),
        new(818, "The Tempest"),
    ];

    private static readonly RouteStop[] endwalkerStops =
    [
        new(956, "Labyrinthos"),
        new(957, "Thavnair"),
        new(958, "Garlemald"),
        new(959, "Mare Lamentorum"),
        new(961, "Elpis"),
        new(960, "Ultima Thule"),
    ];

    private static readonly RouteStop[] dawntrailStops =
    [
        new(1187, "Urqopacha"),
        new(1188, "Kozama'uka"),
        new(1189, "Yak T'el"),
        new(1190, "Shaaloani"),
        new(1191, "Heritage Found"),
        new(1192, "Living Memory"),
    ];

    private static readonly uint[]?[] built = new uint[]?[ExpansionCount];

    public static double MinutesPerZone(ExpansionKind expansion)
        => expansion == ExpansionKind.ARR ? MinutesPerZoneRealmReborn : MinutesPerZoneTwoARanks;

    // The expansion comes from the start zone when the announcement named one, else from its group; a Centurio train
    // with no known start zone could be in any of three expansions and has no route.
    public static bool TryFor(in Announcement announcement, out TrainRoute route)
    {
        route = default;
        if (!TryExpansionOf(announcement, out var expansion))
        {
            return false;
        }

        var zones = Zones(expansion);
        if (zones.Length == 0)
        {
            return false;
        }

        var startIndex = announcement.NamesTerritory ? Array.IndexOf(zones, announcement.TerritoryId) : -1;
        route = new TrainRoute(expansion, zones, Math.Max(0, startIndex));
        return true;
    }

    public static uint[] Zones(ExpansionKind expansion)
    {
        var slot = (int)expansion;
        if ((uint)slot >= ExpansionCount)
        {
            return [];
        }

        return built[slot] ??= Build(expansion);
    }

    private static bool TryExpansionOf(in Announcement announcement, out ExpansionKind expansion)
    {
        if (announcement.NamesTerritory && Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(announcement.TerritoryId) is { } territory)
        {
            expansion = ExpansionKindExtensions.FromExVersion(territory.ExVersion.RowId);
            return true;
        }

        if (ExpansionGroups.ToExpansionKind(announcement.Group) is { } fromGroup)
        {
            expansion = fromGroup;
            return true;
        }

        expansion = default;
        return false;
    }

    private static uint[] Build(ExpansionKind expansion) => expansion switch
    {
        ExpansionKind.HW => Verify(expansion, heavenswardStops),
        ExpansionKind.SB => Verify(expansion, stormbloodStops),
        ExpansionKind.ShB => Verify(expansion, shadowbringersStops),
        ExpansionKind.EW => Verify(expansion, endwalkerStops),
        ExpansionKind.DT => Verify(expansion, dawntrailStops),
        _ => BuildRealmReborn(),
    };

    private static uint[] Verify(ExpansionKind expansion, RouteStop[] stops)
    {
        var territories = Svc.Data.GetExcelSheet<TerritoryType>();
        var placeNames = EnglishPlaceNames();
        var zones = new List<uint>(stops.Length);
        for (var index = 0; index < stops.Length; index++)
        {
            var stop = stops[index];
            if (territories.GetRowOrDefault(stop.TerritoryId) is not { } territory)
            {
                RunLog.Warning($"Train routes: territory {stop.TerritoryId} ({stop.EnglishName}) is not in this client's data; left out of the {expansion.ShortName()} route");
                continue;
            }

            if (placeNames is not null)
            {
                var name = placeNames.GetRowOrDefault(territory.PlaceName.RowId)?.Name.ExtractText() ?? string.Empty;
                if (!string.Equals(name, stop.EnglishName, StringComparison.OrdinalIgnoreCase))
                {
                    RunLog.Warning($"Train routes: territory {stop.TerritoryId} is named '{name}', not '{stop.EnglishName}'; left out of the {expansion.ShortName()} route");
                    continue;
                }
            }

            zones.Add(stop.TerritoryId);
        }

        RunLog.Debug($"Train routes: the {expansion.ShortName()} route has {zones.Count} of {stops.Length} zones");
        return zones.ToArray();
    }

    // Every open-world zone of A Realm Reborn in row order. Wolves' Den Pier is open world too but has no marks, and the
    // minute spent listening there would be lost.
    private static uint[] BuildRealmReborn()
    {
        var territories = Svc.Data.GetExcelSheet<TerritoryType>();
        var zones = new List<uint>();
        for (var rowIndex = 0; rowIndex < territories.Count; rowIndex++)
        {
            var territory = territories.GetRowAt(rowIndex);
            if (territory.TerritoryIntendedUse.RowId != OpenWorldIntendedUse
                || territory.ExVersion.RowId != RealmRebornExVersion
                || territory.NotoriousMonsterTerritory.RowId == 0)
            {
                continue;
            }

            zones.Add(territory.RowId);
        }

        zones.Sort();
        RunLog.Debug($"Train routes: the A Realm Reborn route has {zones.Count} zones");
        return zones.ToArray();
    }

    // Null when this client carries no English data; the ids are then trusted as they are.
    private static ExcelSheet<PlaceName>? EnglishPlaceNames()
    {
        try
        {
            return Svc.Data.GetExcelSheet<PlaceName>(ClientLanguage.English);
        }
        catch (Exception exception)
        {
            RunLog.Debug($"Train routes: English place names are unavailable on this client; the route ids are not checked ({exception.Message})");
            return null;
        }
    }

    private readonly record struct RouteStop(uint TerritoryId, string EnglishName);
}
