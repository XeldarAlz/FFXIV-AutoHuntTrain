using AutoHuntTrain.Core.Marks;

namespace AutoHuntTrain.Core.Feed;

// The four groups trains are announced under; Centurio covers the three expansions whose marks pay Centurio seals.
public enum ExpansionGroup : byte
{
    Centurio,
    Shadowbringers,
    Endwalker,
    Dawntrail,
}

internal static class ExpansionGroups
{
    public const int Count = 4;

    private static readonly char[] listSeparators = [',', '/', '+', '&', ';'];

    private static readonly Alias[] aliases =
    [
        new("Centurio", ExpansionGroup.Centurio),
        new("ARR", ExpansionGroup.Centurio),
        new("A Realm Reborn", ExpansionGroup.Centurio),
        new("HW", ExpansionGroup.Centurio),
        new("Heavensward", ExpansionGroup.Centurio),
        new("SB", ExpansionGroup.Centurio),
        new("StB", ExpansionGroup.Centurio),
        new("Stormblood", ExpansionGroup.Centurio),
        new("ShB", ExpansionGroup.Shadowbringers),
        new("Shadowbringers", ExpansionGroup.Shadowbringers),
        new("EW", ExpansionGroup.Endwalker),
        new("Endwalker", ExpansionGroup.Endwalker),
        new("DT", ExpansionGroup.Dawntrail),
        new("Dawntrail", ExpansionGroup.Dawntrail),
    ];

    private static readonly string[] names = ["Centurio", "Shadowbringers", "Endwalker", "Dawntrail"];

    // The relay's kind is one name, or a list of them; the first one recognized names the group.
    public static bool TryParse(string huntKind, out ExpansionGroup group)
    {
        group = default;
        var start = 0;
        while (start <= huntKind.Length)
        {
            var end = huntKind.IndexOfAny(listSeparators, start);
            if (end < 0)
            {
                end = huntKind.Length;
            }

            if (TryMatch(huntKind.AsSpan(start, end - start).Trim(), out group))
            {
                return true;
            }

            start = end + 1;
        }

        return false;
    }

    public static string Name(ExpansionGroup group)
        => (uint)group < (uint)names.Length ? names[(int)group] : names[0];

    // Centurio spans three expansions, so its kind is only known once a flag names a zone.
    public static ExpansionKind? ToExpansionKind(ExpansionGroup group) => group switch
    {
        ExpansionGroup.Shadowbringers => ExpansionKind.ShB,
        ExpansionGroup.Endwalker => ExpansionKind.EW,
        ExpansionGroup.Dawntrail => ExpansionKind.DT,
        _ => null,
    };

    public static ExpansionGroup FromExpansionKind(ExpansionKind kind) => kind switch
    {
        ExpansionKind.ShB => ExpansionGroup.Shadowbringers,
        ExpansionKind.EW => ExpansionGroup.Endwalker,
        ExpansionKind.DT => ExpansionGroup.Dawntrail,
        _ => ExpansionGroup.Centurio,
    };

    private static bool TryMatch(ReadOnlySpan<char> text, out ExpansionGroup group)
    {
        group = default;
        if (text.Length == 0)
        {
            return false;
        }

        for (var index = 0; index < aliases.Length; index++)
        {
            if (text.Equals(aliases[index].Text, StringComparison.OrdinalIgnoreCase))
            {
                group = aliases[index].Group;
                return true;
            }
        }

        return false;
    }

    private readonly record struct Alias(string Text, ExpansionGroup Group);
}
