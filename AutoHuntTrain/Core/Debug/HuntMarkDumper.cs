using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Travel;
using ECommons.DalamudServices;
using System.Text;

namespace AutoHuntTrain.Core.Debug;

internal static class HuntMarkDumper
{
    public static void Dump()
    {
        var marks = HuntMarkRegistry.Marks;
        Log($"registry: {marks.Length} marks");
        for (var expansion = ExpansionKind.ARR; expansion <= ExpansionKind.DT; expansion++)
        {
            var line = new StringBuilder(expansion.ShortName()).Append(": ").Append(ZoneCount(expansion)).Append(" zones;");
            for (var rank = HuntMarkRank.B; rank <= HuntMarkRank.S; rank++)
            {
                AppendRankCount(line, marks, expansion, rank);
            }

            Log(line.ToString());
        }

        DumpExpansionWide(marks);
        Svc.Chat.Print($"{AhtConstants.LogPrefix} Hunt mark dump written to the plugin log (/xllog).");
    }

    private static void AppendRankCount(StringBuilder line, ReadOnlySpan<HuntMark> marks, ExpansionKind expansion, HuntMarkRank rank)
    {
        var count = 0;
        var covered = 0;
        for (var index = 0; index < marks.Length; index++)
        {
            var mark = marks[index];
            if (mark.Expansion != expansion || mark.Rank != rank)
            {
                continue;
            }

            count++;
            if (HuntSpawns.Covers(mark.NameId, HuntMarkRegistry.SpawnTerritoryAt(index)))
            {
                covered++;
            }
        }

        line.Append(' ').Append(rank).Append(' ').Append(count).Append(" (").Append(covered).Append(" with zone spawn points)");
    }

    private static void DumpExpansionWide(ReadOnlySpan<HuntMark> marks)
    {
        for (var index = 0; index < marks.Length; index++)
        {
            if (!HuntMarkRegistry.IsExpansionWideAt(index))
            {
                continue;
            }

            var mark = marks[index];
            Log($"expansion-wide: {HuntMarkRegistry.NameAt(index)} (BNpcName {mark.NameId}, rank {mark.Rank}, {mark.Expansion.ShortName()}, first listed in {TerritoryNames.Of(mark.TerritoryId)} ({mark.TerritoryId})); only a flag can place it");
        }
    }

    // Expansion-wide marks keep the first zone that lists them, so they are left out of the zone count.
    private static int ZoneCount(ExpansionKind expansion)
    {
        var marks = HuntMarkRegistry.Marks;
        var order = HuntMarkRegistry.DisplayOrder;
        var zones = 0;
        uint previousTerritory = 0;
        for (var position = 0; position < order.Length; position++)
        {
            var markIndex = order[position];
            var mark = marks[markIndex];
            if (mark.Expansion != expansion || HuntMarkRegistry.IsExpansionWideAt(markIndex) || mark.TerritoryId == previousTerritory)
            {
                continue;
            }

            zones++;
            previousTerritory = mark.TerritoryId;
        }

        return zones;
    }

    private static void Log(string message) => RunLog.Info(message);
}
