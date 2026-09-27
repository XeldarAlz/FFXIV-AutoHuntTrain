namespace AutoHuntTrain.Core.Marks;

// How many marks a full train credits, per expansion.
internal static class ExpectedMarks
{
    public const int Unknown = 0;

    // The A ranks of each expansion's open-world zones, indexed by ExpansionKind: one per zone over A Realm Reborn's 17
    // zones, two per zone over the six zones of every later expansion.
    private static readonly int[] aRanksPerExpansion = [17, 12, 12, 12, 12, 12];

    public static int For(ExpansionKind expansion)
        => (uint)expansion < (uint)aRanksPerExpansion.Length ? aRanksPerExpansion[(int)expansion] : Unknown;

    public static int For(ExpansionKind? expansion) => expansion is { } known ? For(known) : Unknown;
}
