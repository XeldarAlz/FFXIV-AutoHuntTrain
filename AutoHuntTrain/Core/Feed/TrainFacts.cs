using AutoHuntTrain.Core.Travel;
using System.Numerics;

namespace AutoHuntTrain.Core.Feed;

// Where a train starts, for its details, its chat line and its relay: the zone and aetheryte the relay's ids resolved
// to, else the names its header gave, and the map spot to flag.
internal static class TrainFacts
{
    public static string StartZone(in Announcement announcement)
        => announcement.NamesTerritory
            ? TerritoryNames.Of(announcement.TerritoryId)
            : AnnouncementBody.ReadHeaderField(announcement.Message, AnnouncementBody.StartZoneLabel);

    public static string Aetheryte(in Announcement announcement)
        => announcement.NamesAetheryte && ZoneAetherytes.TryFindById(announcement.AetheryteId, out _, out var aetheryte)
            ? aetheryte.Name
            : AnnouncementBody.ReadHeaderField(announcement.Message, AnnouncementBody.AetheryteLabel);

    // The relay's own coordinates first, else the first pair the post gives; a spot needs a known zone to be flagged in.
    public static bool TryFlagPoint(in Announcement announcement, out Vector2 coordinates)
    {
        coordinates = default;
        if (!announcement.NamesTerritory)
        {
            return false;
        }

        if (announcement.MapCoordinates is { } relayed)
        {
            coordinates = relayed;
            return true;
        }

        return AnnouncementBody.TryReadCoordinates(announcement.Message, out coordinates);
    }
}
