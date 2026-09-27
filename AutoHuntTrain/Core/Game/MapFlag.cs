using AutoHuntTrain.Core.Travel;
using Dalamud.Game.Text.SeStringHandling.Payloads;
using ECommons.DalamudServices;
using FFXIVClientStructs.FFXIV.Client.UI.Agent;
using Lumina.Excel.Sheets;
using System.Numerics;

namespace AutoHuntTrain.Core.Game;

// The map flag on a spot given in map coordinates. Opening the map is ported from HuntAlerts' MapManager: a map link
// payload opens the zone's map at the spot and places the flag there.
internal static class MapFlag
{
    public static bool TryOpenMap(uint territoryId, Vector2 mapCoordinates)
    {
        if (!TryMapId(territoryId, out var mapId))
        {
            return false;
        }

        return Svc.GameGui.OpenMapWithMapLink(new MapLinkPayload(territoryId, mapId, mapCoordinates.X, mapCoordinates.Y));
    }

    // Places the flag without opening the map, so a chat line's <flag> points at the spot.
    public static unsafe bool TrySetFlag(uint territoryId, Vector2 mapCoordinates)
    {
        if (!TryMapId(territoryId, out var mapId) || !MapCoordinates.TryToWorld(territoryId, mapCoordinates, out var world))
        {
            return false;
        }

        var agent = AgentMap.Instance();
        if (agent is null)
        {
            return false;
        }

        agent->SetFlagMapMarker(territoryId, mapId, world);
        return true;
    }

    private static bool TryMapId(uint territoryId, out uint mapId)
    {
        mapId = Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId)?.Map.RowId ?? 0;
        return mapId != 0;
    }
}
