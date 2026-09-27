using AutoHuntTrain.Core.Feed;

namespace AutoHuntTrain.Core.Tasks;

internal sealed partial class AutoHuntController
{
    private const int FeedEvaluationIntervalMs = 1_000;

    private long nextFeedEvaluationAtMs;
    private int lastRiddenAnnouncementId;
    private int autoRideRefusedId;

    // Once a second while auto-ride is on and nothing runs: the soonest announced train that passes every rule is
    // ridden. The train of the last ride and the one whose start was refused are left alone, so neither is retried
    // every second. Nothing here allocates unless a ride starts.
    private void TickAutoRide(long now)
    {
        if (now < nextFeedEvaluationAtMs)
        {
            return;
        }

        nextFeedEvaluationAtMs = now + FeedEvaluationIntervalMs;
        var configuration = Plugin.Instance.Configuration;
        if (!configuration.AutoRide || Running || !ECommons.DalamudServices.Svc.ClientState.IsLoggedIn)
        {
            return;
        }

        var nowUtc = DateTime.UtcNow;
        if (configuration.IsSnoozed(nowUtc))
        {
            return;
        }

        var feed = Plugin.Instance.Feed;
        for (var index = 0; index < feed.Count; index++)
        {
            var announcement = feed[index];
            if (announcement.Id == lastRiddenAnnouncementId || announcement.Id == autoRideRefusedId)
            {
                continue;
            }

            if (RideRules.Evaluate(announcement, nowUtc, forAutoRide: true, out _) != RideVerdict.Rideable)
            {
                continue;
            }

            Diag($"Auto-ride: the {ExpansionGroups.Name(announcement.Group)} train on {announcement.World.Name} starts in {announcement.LeadAt(nowUtc).TotalMinutes:F0} min and passes every rule; riding it.");
            if (!StartRide(announcement))
            {
                autoRideRefusedId = announcement.Id;
            }

            return;
        }
    }
}
