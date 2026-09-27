using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using Newtonsoft.Json;
using System.IO;
using System.Numerics;

namespace AutoHuntTrain.Core.Feed;

// Keeps the announced trains on disk, so a plugin reload or a game restart does not empty the list until the next
// alert arrives. Worlds are stored by id and rebuilt from the game's sheet on load.
internal static class FeedStore
{
    private const string FileName = "AutoHuntTrain.Feed.json";

    private static string FilePath
        => Path.Combine(Plugin.PluginInterface.ConfigDirectory.FullName, FileName);

    public static void Save(ReadOnlySpan<Announcement> announcements)
    {
        var stored = new StoredAnnouncement[announcements.Length];
        for (var index = 0; index < announcements.Length; index++)
        {
            stored[index] = StoredAnnouncement.From(announcements[index]);
        }

        try
        {
            var directory = Plugin.PluginInterface.ConfigDirectory;
            if (!directory.Exists)
            {
                directory.Create();
            }

            File.WriteAllText(FilePath, JsonConvert.SerializeObject(stored));
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Feed: saving the train list failed");
        }
    }

    public static StoredAnnouncement[] Load()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return [];
            }

            return JsonConvert.DeserializeObject<StoredAnnouncement[]>(File.ReadAllText(FilePath)) ?? [];
        }
        catch (Exception exception)
        {
            RunLog.Warning(exception, "Feed: reading the saved train list failed; starting empty");
            return [];
        }
    }
}

internal sealed class StoredAnnouncement
{
    public DateTime ReceivedAtUtc { get; set; }
    public DateTime PostedAtUtc { get; set; }
    public uint WorldId { get; set; }
    public ExpansionGroup Group { get; set; }
    public DateTime StartAtUtc { get; set; }
    public uint AetheryteId { get; set; }
    public uint TerritoryId { get; set; }
    public int Instance { get; set; }
    public float? MapX { get; set; }
    public float? MapY { get; set; }
    public string ConductorName { get; set; } = string.Empty;
    public uint ConductorWorldId { get; set; }
    public string Message { get; set; } = string.Empty;

    public static StoredAnnouncement From(in Announcement announcement) => new()
    {
        ReceivedAtUtc = announcement.ReceivedAtUtc,
        PostedAtUtc = announcement.PostedAtUtc,
        WorldId = announcement.World.Id,
        Group = announcement.Group,
        StartAtUtc = announcement.StartAtUtc,
        AetheryteId = announcement.AetheryteId,
        TerritoryId = announcement.TerritoryId,
        Instance = announcement.Instance,
        MapX = announcement.MapCoordinates?.X,
        MapY = announcement.MapCoordinates?.Y,
        ConductorName = announcement.Conductor.Name ?? string.Empty,
        ConductorWorldId = announcement.Conductor.WorldId,
        Message = announcement.Message ?? string.Empty,
    };

    // A world the client no longer knows makes the train unusable, so it is dropped rather than shown half built.
    public bool TryRebuild(int id, out Announcement announcement)
    {
        announcement = default;
        if (!Worlds.TryFindById(WorldId, out var world))
        {
            return false;
        }

        Vector2? coordinates = MapX is { } x && MapY is { } y ? new Vector2(x, y) : null;
        var conductor = new ConductorIdentity(ConductorName ?? string.Empty, ConductorWorldId);
        announcement = new Announcement(
            id,
            DateTime.SpecifyKind(ReceivedAtUtc, DateTimeKind.Utc),
            DateTime.SpecifyKind(PostedAtUtc, DateTimeKind.Utc),
            world,
            Group,
            DateTime.SpecifyKind(StartAtUtc, DateTimeKind.Utc),
            AetheryteId,
            TerritoryId,
            Instance,
            coordinates,
            conductor,
            Message ?? string.Empty);
        return true;
    }
}
