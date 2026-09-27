using ECommons.DalamudServices;
using Lumina.Excel.Sheets;

namespace AutoHuntTrain.Core.Marks;

internal static class HuntMarkRegistry
{
    public const int NotFound = -1;

    private const uint OpenWorldIntendedUse = 1;
    private const uint AnyTerritory = 0;

    private static Tables? tables;

    private static Tables Data => tables ??= new TableBuilder().Build();

    public static ReadOnlySpan<HuntMark> Marks => Data.Marks;

    public static ReadOnlySpan<int> DisplayOrder => Data.DisplayOrder;

    public static int IndexOf(uint nameId)
    {
        var index = Data.NameIds.AsSpan().BinarySearch(nameId);
        return index < 0 ? NotFound : index;
    }

    public static bool TryGet(uint nameId, out HuntMark mark)
    {
        var index = IndexOf(nameId);
        if (index == NotFound)
        {
            mark = default;
            return false;
        }

        mark = Data.Marks[index];
        return true;
    }

    public static bool IsHuntMark(uint nameId) => IndexOf(nameId) != NotFound;

    // The SS marks and their minions spawn in any zone of their expansion; the zone kept for them is only the first that
    // lists them.
    public static bool IsExpansionWideAt(int index)
    {
        var expansionWide = Data.ExpansionWide;
        return (uint)index < (uint)expansionWide.Length && expansionWide[index];
    }

    // An S rank, and every expansion-wide mark, is up only after an in-game trigger.
    public static bool AppearsOnTrigger(uint nameId)
    {
        var index = IndexOf(nameId);
        return index != NotFound && (Data.Marks[index].Rank == HuntMarkRank.S || Data.ExpansionWide[index]);
    }

    // The zone to read a mark's spawn points in; 0, which spawn lookups read as every zone, for an expansion-wide mark.
    public static uint SpawnTerritoryAt(int index)
    {
        var marks = Data.Marks;
        return (uint)index < (uint)marks.Length && !Data.ExpansionWide[index] ? marks[index].TerritoryId : AnyTerritory;
    }

    public static uint SpawnTerritoryOf(uint nameId) => SpawnTerritoryAt(IndexOf(nameId));

    public static int RankBit(HuntMarkRank rank) => 1 << (int)rank;

    // A name the registry does not list is read from the sheet, so a log line never shows a bare id.
    public static string NameOf(uint nameId)
    {
        var index = IndexOf(nameId);
        return index == NotFound ? GameText.NpcNameOrId(Svc.Data.GetExcelSheet<BNpcName>(), nameId) : Data.Names[index];
    }

    public static string NameAt(int index)
    {
        var names = Data.Names;
        return (uint)index < (uint)names.Length ? names[index] : string.Empty;
    }

    private readonly record struct Listing(HuntMark Mark, uint RegionId, int Ordinal);

    private sealed class Tables
    {
        public required HuntMark[] Marks { get; init; }
        public required uint[] NameIds { get; init; }
        public required string[] Names { get; init; }
        public required bool[] ExpansionWide { get; init; }
        public required int[] DisplayOrder { get; init; }
    }

    private sealed class TableBuilder
    {
        private readonly List<Listing> listings = [];
        private readonly Dictionary<uint, ushort> territoryByName = [];
        private readonly HashSet<uint> repeatedNames = [];
        private readonly HashSet<ushort> zones = [];
        private int repeatListings;

        public Tables Build()
        {
            Collect();
            var byName = listings.ToArray();
            Array.Sort(byName, CompareByName);
            var count = byName.Length;
            var marks = new HuntMark[count];
            var nameIds = new uint[count];
            var names = new string[count];
            var expansionWide = new bool[count];
            var npcNames = Svc.Data.GetExcelSheet<BNpcName>();
            for (var index = 0; index < count; index++)
            {
                var mark = byName[index].Mark;
                marks[index] = mark;
                nameIds[index] = mark.NameId;
                names[index] = GameText.NpcNameOrId(npcNames, mark.NameId);
                expansionWide[index] = repeatedNames.Contains(mark.NameId);
            }

            var displayOrder = BuildDisplayOrder(byName, expansionWide);
            RunLog.Info($"Hunt mark registry: {count} marks over {zones.Count} open-world zones ({repeatedNames.Count} expansion-wide); {repeatListings} repeated listings skipped");
            return new Tables
            {
                Marks = marks,
                NameIds = nameIds,
                Names = names,
                ExpansionWide = expansionWide,
                DisplayOrder = displayOrder,
            };
        }

        private void Collect()
        {
            var sheet = Svc.Data.GetExcelSheet<TerritoryType>();
            for (var rowIndex = 0; rowIndex < sheet.Count; rowIndex++)
            {
                var territory = sheet.GetRowAt(rowIndex);
                if (territory.TerritoryIntendedUse.RowId != OpenWorldIntendedUse || territory.RowId > ushort.MaxValue)
                {
                    continue;
                }

                if (territory.NotoriousMonsterTerritory.RowId == 0 || territory.NotoriousMonsterTerritory.ValueNullable is not { } list)
                {
                    continue;
                }

                AddTerritory(territory, list);
            }
        }

        private void AddTerritory(in TerritoryType territory, in NotoriousMonsterTerritory list)
        {
            var territoryId = (ushort)territory.RowId;
            zones.Add(territoryId);
            var regionId = territory.PlaceNameRegion.RowId;
            var expansion = ExpansionKindExtensions.FromExVersion(territory.ExVersion.RowId);
            var monsters = list.NotoriousMonsters;
            for (var slot = 0; slot < monsters.Count; slot++)
            {
                var reference = monsters[slot];
                if (reference.RowId == 0 || reference.ValueNullable is not { } monster || monster.BNpcName.RowId == 0)
                {
                    continue;
                }

                AddMark(monster, territoryId, regionId, expansion);
            }
        }

        private void AddMark(in NotoriousMonster monster, ushort territoryId, uint regionId, ExpansionKind expansion)
        {
            var nameId = monster.BNpcName.RowId;
            if (monster.Rank < (byte)HuntMarkRank.B || monster.Rank > (byte)HuntMarkRank.S)
            {
                RunLog.Warning($"Hunt mark registry: NotoriousMonster {monster.RowId} (BNpcName {nameId}) has rank {monster.Rank}, not B, A or S; skipped");
                return;
            }

            if (!territoryByName.TryAdd(nameId, territoryId))
            {
                SkipRepeat(nameId, territoryId);
                return;
            }

            listings.Add(new Listing(new HuntMark(nameId, (HuntMarkRank)monster.Rank, territoryId, expansion), regionId, listings.Count));
        }

        // The expansion-wide SS marks and their minions sit in every zone's list of their expansion, some several times
        // per zone, so each name is reported once, keeps the first zone it appears in, and is flagged as expansion-wide.
        private void SkipRepeat(uint nameId, ushort territoryId)
        {
            repeatListings++;
            if (!repeatedNames.Add(nameId))
            {
                return;
            }

            RunLog.Info($"Hunt mark registry: BNpcName {nameId} is listed again in territory {territoryId}; it keeps territory {territoryByName[nameId]} and further listings are skipped");
        }

        private static int[] BuildDisplayOrder(Listing[] byName, bool[] expansionWide)
        {
            var order = new int[byName.Length];
            for (var index = 0; index < order.Length; index++)
            {
                order[index] = index;
            }

            Array.Sort(order, (left, right) => CompareForDisplay(byName[left], expansionWide[left], byName[right], expansionWide[right]));
            return order;
        }

        private static int CompareByName(Listing left, Listing right) => left.Mark.NameId.CompareTo(right.Mark.NameId);

        // Region before territory keeps a region's zones together; the expansion-wide marks follow every zone of their
        // expansion.
        private static int CompareForDisplay(in Listing left, bool leftExpansionWide, in Listing right, bool rightExpansionWide)
        {
            var byExpansion = ((int)left.Mark.Expansion).CompareTo((int)right.Mark.Expansion);
            if (byExpansion != 0)
            {
                return byExpansion;
            }

            var byReach = leftExpansionWide.CompareTo(rightExpansionWide);
            if (byReach != 0)
            {
                return byReach;
            }

            var byRegion = left.RegionId.CompareTo(right.RegionId);
            if (byRegion != 0)
            {
                return byRegion;
            }

            var byZone = left.Mark.TerritoryId.CompareTo(right.Mark.TerritoryId);
            if (byZone != 0)
            {
                return byZone;
            }

            var byRank = ((byte)left.Mark.Rank).CompareTo((byte)right.Mark.Rank);
            return byRank != 0 ? byRank : left.Ordinal.CompareTo(right.Ordinal);
        }
    }
}
