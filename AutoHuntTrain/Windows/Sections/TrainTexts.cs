using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Localization;
using System.Numerics;

namespace AutoHuntTrain.Windows.Sections;

// Texts the train list and a train's details both show, cached so drawing them every frame allocates nothing.
internal static class TrainTexts
{
    private static CachedText verdictTooltip;

    public static string Countdown(ref CachedText cache, in Announcement announcement, DateTime nowUtc, out Vector4 color)
    {
        var minutes = TrainFacts.WholeMinutesToStart(announcement, nowUtc);
        color = minutes < 0 ? Styling.AccentAmber : minutes == 0 ? Styling.AccentMintSoft : Styling.TextSecondary;
        var key = HashCode.Combine(announcement.Id, minutes);
        if (cache.TryGet(key, out var text))
        {
            return text;
        }

        return cache.Set(key, TrainFacts.CountdownText(minutes));
    }

    public static string Verdict(RideVerdict verdict, in Announcement announcement)
    {
        if (verdict != RideVerdict.NotAllowedDataCenter)
        {
            return Loc.T(VerdictEntry(verdict));
        }

        var key = HashCode.Combine(announcement.Id, (int)verdict);
        if (verdictTooltip.TryGet(key, out var text))
        {
            return text;
        }

        return verdictTooltip.Set(key, Loc.T(L.Feed.VerdictNotAllowedDataCenter, announcement.World.DataCenterName));
    }

    private static LocString VerdictEntry(RideVerdict verdict) => verdict switch
    {
        RideVerdict.NotEnabledGroup => L.Feed.VerdictNotEnabledGroup,
        RideVerdict.OutOfRegion => L.Feed.VerdictOutOfRegion,
        RideVerdict.CrossDataCenterOff => L.Feed.VerdictCrossDataCenterOff,
        RideVerdict.TooSoon => L.Feed.VerdictTooSoon,
        RideVerdict.TooLate => L.Feed.VerdictTooLate,
        RideVerdict.InDuty => L.Feed.VerdictInDuty,
        RideVerdict.LifestreamBusy => L.Feed.VerdictLifestreamBusy,
        RideVerdict.RideRunning => L.Feed.VerdictRideRunning,
        RideVerdict.Snoozed => L.Feed.VerdictSnoozed,
        _ => L.Feed.VerdictFeedWorldUnknown,
    };
}
