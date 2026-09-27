using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Localization;

namespace AutoHuntTrain.Windows.Sections;

// Which expansions auto-join takes, and until when it is snoozed; rebuilt only when either changes, so drawing it
// every frame allocates nothing.
internal static class FeedStatusLine
{
    private const string ClockFormat = "HH:mm";

    // Newest expansion first, the order a player scans for.
    private static readonly ExpansionGroup[] listOrder =
        [ExpansionGroup.Dawntrail, ExpansionGroup.Endwalker, ExpansionGroup.Shadowbringers, ExpansionGroup.Centurio];

    private static CachedText text;

    public static string Get(Configuration configuration)
    {
        var snoozed = configuration.IsSnoozed(DateTime.UtcNow);
        var untilMinute = snoozed ? configuration.SnoozeUntilUtc!.Value.Ticks / TimeSpan.TicksPerMinute : 0;
        var mask = EnabledMask(configuration);
        var key = HashCode.Combine(mask, snoozed, untilMinute);
        if (text.TryGet(key, out var line))
        {
            return line;
        }

        var state = State(mask);
        if (!snoozed)
        {
            return text.Set(key, state);
        }

        var until = configuration.SnoozeUntilUtc!.Value.ToLocalTime().ToString(ClockFormat, Loc.Culture);
        return text.Set(key, Loc.T(L.Feed.SnoozedUntil, state, until));
    }

    private static int EnabledMask(Configuration configuration)
    {
        var mask = 0;
        for (var index = 0; index < listOrder.Length; index++)
        {
            if (configuration.IsGroupEnabled(listOrder[index]))
            {
                mask |= 1 << index;
            }
        }

        return mask;
    }

    private static string State(int mask)
    {
        if (mask == 0)
        {
            return Loc.T(L.Feed.AutoJoinOff);
        }

        if (mask == (1 << listOrder.Length) - 1)
        {
            return Loc.T(L.Feed.AutoJoinEvery);
        }

        var separator = Loc.T(L.Feed.AutoJoinSeparator);
        var names = string.Empty;
        for (var index = 0; index < listOrder.Length; index++)
        {
            if ((mask & (1 << index)) == 0)
            {
                continue;
            }

            var name = ExpansionGroups.LocalName(listOrder[index]);
            names = names.Length == 0 ? name : string.Concat(names, separator, name);
        }

        return Loc.T(L.Feed.AutoJoinOn, names);
    }
}
