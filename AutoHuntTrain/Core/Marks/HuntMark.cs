using AutoHuntTrain.Core.Hunts;

namespace AutoHuntTrain.Core.Marks;

public readonly record struct HuntMark(
    uint NameId,
    HuntMarkRank Rank,
    ushort TerritoryId,
    ExpansionKind Expansion);
