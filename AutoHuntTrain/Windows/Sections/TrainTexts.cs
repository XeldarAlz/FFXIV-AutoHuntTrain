using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Train;
using System.Numerics;

namespace AutoHuntTrain.Windows.Sections;

// Texts the train list and a train's details both show, cached so drawing them every frame allocates nothing.
internal static class TrainTexts
{
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

    // A ride on a train in progress catches up with it, unless there is no route to catch up along.
    public static bool CatchesUp(in Announcement announcement, DateTime nowUtc)
        => announcement.InProgressAt(nowUtc) && TrainRoutes.TryFor(announcement, out _);

    public static string RideHint(bool catchesUp) => Loc.T(catchesUp ? L.Feed.RideCatchUpHint : L.Feed.RideHint);

    // A Ride button is only ever refused for what makes a ride impossible right now.
    public static string Verdict(RideVerdict verdict) => Loc.T(verdict switch
    {
        RideVerdict.OutOfRegion => L.Feed.VerdictOutOfRegion,
        RideVerdict.InDuty => L.Feed.VerdictInDuty,
        RideVerdict.LifestreamBusy => L.Feed.VerdictLifestreamBusy,
        RideVerdict.RideRunning => L.Feed.VerdictRideRunning,
        _ => L.Feed.VerdictFeedWorldUnknown,
    });
}
