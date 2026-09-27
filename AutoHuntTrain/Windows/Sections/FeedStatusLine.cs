using AutoHuntTrain.Core.Localization;

namespace AutoHuntTrain.Windows.Sections;

// Whether auto-ride is on, and until when it is snoozed; rebuilt only when either changes, so drawing it every frame
// allocates nothing.
internal static class FeedStatusLine
{
    private const string ClockFormat = "HH:mm";

    private static CachedText text;

    public static string Get(Configuration configuration)
    {
        var snoozed = configuration.IsSnoozed(DateTime.UtcNow);
        var untilMinute = snoozed ? configuration.SnoozeUntilUtc!.Value.Ticks / TimeSpan.TicksPerMinute : 0;
        var key = HashCode.Combine(configuration.AutoRide, snoozed, untilMinute);
        if (text.TryGet(key, out var line))
        {
            return line;
        }

        var state = Loc.T(configuration.AutoRide ? L.Feed.AutoRideOn : L.Feed.AutoRideOff);
        if (!snoozed)
        {
            return text.Set(key, state);
        }

        var until = configuration.SnoozeUntilUtc!.Value.ToLocalTime().ToString(ClockFormat, Loc.Culture);
        return text.Set(key, Loc.T(L.Feed.SnoozedUntil, state, until));
    }
}
