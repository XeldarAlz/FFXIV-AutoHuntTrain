using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AutoHuntTrain.Core.Travel;

// The inverse of the game's world to map projection, for a relay that names a spot by its map coordinates alone.
internal static class MapCoordinates
{
    private const float MapTiles = 41f;
    private const float MapPixels = 2048f;
    private const float MapHalfPixels = 1024f;
    private const float PercentScale = 100f;

    public static bool TryToWorld(uint territoryId, Vector2 mapCoordinates, out Vector3 world)
    {
        world = default;
        if (Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId)?.Map.ValueNullable is not { } map)
        {
            return false;
        }

        var scale = map.SizeFactor / PercentScale;
        if (scale <= 0f)
        {
            return false;
        }

        world = new Vector3(ToWorld(mapCoordinates.X, scale, map.OffsetX), 0f, ToWorld(mapCoordinates.Y, scale, map.OffsetY));
        return float.IsFinite(world.X) && float.IsFinite(world.Z);
    }

    // Undoes map = 41 / scale * ((world + offset) * scale + 1024) / 2048 + 1.
    private static float ToWorld(float mapCoordinate, float scale, int offset)
        => ((mapCoordinate - 1f) * scale / MapTiles * MapPixels - MapHalfPixels) / scale - offset;
}
