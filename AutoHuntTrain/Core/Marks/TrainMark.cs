using System.Numerics;

namespace AutoHuntTrain.Core.Marks;

// A mark a ride goes after. A flagged position comes from the conductor's map flag, with no height, and is searched
// first; without one the mark is looked for over every spawn point of its rank in its zone.
public readonly record struct TrainMark(uint NameId, uint TerritoryId, Vector3 FlaggedPosition)
{
    private static readonly Vector3 noFlag = new(float.NaN);

    public static TrainMark Unflagged(uint nameId, uint territoryId) => new(nameId, territoryId, noFlag);

    public static TrainMark Flagged(uint nameId, uint territoryId, float x, float z) => new(nameId, territoryId, new Vector3(x, float.NaN, z));

    public bool HasFlag => !float.IsNaN(FlaggedPosition.X);
}
