using System.Numerics;

namespace AutoHuntTrain.Core.Travel;

// Ported from HuntAlerts' ArrowWaypoint: the one spot the on-screen arrow points at, in one zone on one world, for
// half an hour at most.
internal static class NavWaypoint
{
    private const long LifetimeMs = 30L * 60L * 1_000L;

    private static long expiresAtMs;

    public static int AnnouncementId { get; private set; }

    public static uint TerritoryId { get; private set; }

    public static uint WorldId { get; private set; }

    public static Vector2 MapCoordinates { get; private set; }

    public static Vector3 WorldPosition { get; private set; }

    public static bool IsActive => TerritoryId != 0 && Environment.TickCount64 <= expiresAtMs;

    public static bool IsFor(int announcementId) => IsActive && AnnouncementId == announcementId;

    public static bool TrySet(int announcementId, uint territoryId, uint worldId, Vector2 mapCoordinates)
    {
        if (!Travel.MapCoordinates.TryToWorld(territoryId, mapCoordinates, out var world))
        {
            return false;
        }

        AnnouncementId = announcementId;
        TerritoryId = territoryId;
        WorldId = worldId;
        MapCoordinates = mapCoordinates;
        WorldPosition = world;
        expiresAtMs = Environment.TickCount64 + LifetimeMs;
        RunLog.Info($"Nav: pointing at ({mapCoordinates.X:F1}, {mapCoordinates.Y:F1}) in {TerritoryNames.Of(territoryId)}");
        return true;
    }

    public static void Clear()
    {
        if (TerritoryId == 0)
        {
            return;
        }

        RunLog.Info("Nav: the arrow is cleared");
        AnnouncementId = 0;
        TerritoryId = 0;
        WorldId = 0;
    }
}
