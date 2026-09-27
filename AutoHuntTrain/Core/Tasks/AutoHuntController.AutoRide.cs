using AutoHuntTrain.Core.Feed;

namespace AutoHuntTrain.Core.Tasks;

internal sealed partial class AutoHuntController
{
    private const int FeedEvaluationIntervalMs = 1_000;

    // Every train auto-ride or the player has already taken, as many as the feed can hold, so auto-ride never swaps
    // back and forth between two trains when the player stops each one.
    private readonly int[] takenAnnouncementIds = new int[FeedListener.Capacity];

    private long nextFeedEvaluationAtMs;
    private int lastRiddenAnnouncementId;
    private int autoRideRefusedId;
    private int nextTakenSlot;
    private int autoRideHoldBelowId;

    // The player stopping a ride is a decision about auto-ride too: it waits for a train announced after the stop
    // instead of taking the next one in the list a second later.
    public void StopByPlayer()
    {
        var riding = Running;
        Stop();
        if (!riding || !Plugin.Instance.Configuration.AutoRide)
        {
            return;
        }

        autoRideHoldBelowId = Plugin.Instance.Feed.NextId;
        Diag("Auto-ride: the player stopped the ride; waiting for a train announced from now on.");
        ECommons.DalamudServices.Svc.Chat.Print($"{AhtConstants.LogPrefix} Auto-ride will wait for the next train announced from now on.");
    }

    private void RememberTaken(int announcementId)
    {
        takenAnnouncementIds[nextTakenSlot] = announcementId;
        nextTakenSlot = (nextTakenSlot + 1) % takenAnnouncementIds.Length;
    }

    private bool WasTaken(int announcementId)
    {
        for (var index = 0; index < takenAnnouncementIds.Length; index++)
        {
            if (takenAnnouncementIds[index] == announcementId)
            {
                return true;
            }
        }

        return false;
    }

    // Once a second while auto-ride is on and nothing runs: the soonest announced train that passes every rule is
    // ridden. A train already taken, one whose start was refused, and anything announced before the player's last
    // Stop are left alone. Nothing here allocates unless a ride starts.
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
            if (announcement.Id < autoRideHoldBelowId || announcement.Id == autoRideRefusedId || WasTaken(announcement.Id))
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
