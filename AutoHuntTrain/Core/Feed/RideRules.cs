using AutoHuntTrain.Core.Ipc;
using AutoHuntTrain.Core.Travel;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace AutoHuntTrain.Core.Feed;

internal enum RideVerdict : byte
{
    Rideable,
    NotEnabledGroup,
    NotAllowedDataCenter,
    OutOfRegion,
    CrossDataCenterOff,
    TooSoon,
    TooLate,
    InDuty,
    LifestreamBusy,
    RideRunning,
    Snoozed,
    FeedWorldUnknown,
}

internal enum Reachability : byte
{
    SameWorld,
    SameDataCenter,
    CrossDataCenter,
    OutOfRegion,
}

// Whether an announced train is one to ride, from the player's rules and the character's state, in the order a
// player would explain a refusal: what is running, the rules, the clock, then what the character is doing right now.
internal static class RideRules
{
    private static readonly TimeSpan AutoRideJoinWindow = TimeSpan.FromMinutes(10);

    public static RideVerdict Evaluate(in Announcement announcement)
        => Evaluate(announcement, DateTime.UtcNow, forAutoRide: false, out _);

    public static RideVerdict Evaluate(in Announcement announcement, DateTime nowUtc, bool forAutoRide, out Reachability reachability)
        => Evaluate(announcement, nowUtc, forAutoRide, ignoreRunningRide: false, out reachability);

    // Asked by a ride that has just ended, while it may still hold the controller, about the train auto-ride would
    // take next; that ride is not a reason to refuse.
    public static RideVerdict EvaluateNextTrain(in Announcement announcement, DateTime nowUtc, out Reachability reachability)
        => Evaluate(announcement, nowUtc, forAutoRide: true, ignoreRunningRide: true, out reachability);

    private static RideVerdict Evaluate(in Announcement announcement, DateTime nowUtc, bool forAutoRide, bool ignoreRunningRide, out Reachability reachability)
    {
        var configuration = Plugin.Instance.Configuration;
        reachability = Reachability.OutOfRegion;
        if (announcement.World.Id == 0)
        {
            return RideVerdict.FeedWorldUnknown;
        }

        if (!ignoreRunningRide && Plugin.Instance.Controller.Running)
        {
            return RideVerdict.RideRunning;
        }

        if (forAutoRide && configuration.IsSnoozed(nowUtc))
        {
            return RideVerdict.Snoozed;
        }

        if (!configuration.IsGroupEnabled(announcement.Group))
        {
            return RideVerdict.NotEnabledGroup;
        }

        if (!TryReachability(announcement.World, out reachability) || reachability == Reachability.OutOfRegion)
        {
            return RideVerdict.OutOfRegion;
        }

        if (!DataCenterAllowed(configuration, announcement.World))
        {
            return RideVerdict.NotAllowedDataCenter;
        }

        if (reachability == Reachability.CrossDataCenter && !configuration.AllowCrossDataCenterRides)
        {
            return RideVerdict.CrossDataCenterOff;
        }

        var timing = Timing(configuration, announcement.LeadAt(nowUtc), reachability, forAutoRide);
        if (timing != RideVerdict.Rideable)
        {
            return timing;
        }

        if (InDuty())
        {
            return RideVerdict.InDuty;
        }

        if (LifestreamIPC.Instance.IsBusy())
        {
            return RideVerdict.LifestreamBusy;
        }

        return RideVerdict.Rideable;
    }

    // Whether a train belongs in the list at all: its expansion group is on and its data center is one the player
    // allows. The rest of the rules only decide whether it can be ridden right now.
    public static bool IsListed(in Announcement announcement)
    {
        var configuration = Plugin.Instance.Configuration;
        return announcement.World.Id != 0
            && configuration.IsGroupEnabled(announcement.Group)
            && DataCenterAllowed(configuration, announcement.World);
    }

    public static bool TryReachability(in WorldInfo target, out Reachability reachability)
    {
        reachability = Reachability.OutOfRegion;
        if (!Worlds.TryCurrent(out var current))
        {
            return false;
        }

        if (current.Id == target.Id)
        {
            reachability = Reachability.SameWorld;
        }
        else if (Worlds.SameDataCenter(current, target))
        {
            reachability = Reachability.SameDataCenter;
        }
        else if (Worlds.SameRegion(current, target))
        {
            reachability = Reachability.CrossDataCenter;
        }

        return true;
    }

    // The clock only ever refuses auto-ride; a train the player clicks is the player's call. A data center transfer
    // takes minutes plus a queue, so a train there is committed to only with the configured lead, and a train that
    // started longer ago than the join window has lost most of its marks.
    private static RideVerdict Timing(Configuration configuration, TimeSpan lead, Reachability reachability, bool forAutoRide)
    {
        if (!forAutoRide)
        {
            return RideVerdict.Rideable;
        }

        if (lead <= TimeSpan.Zero)
        {
            return -lead > AutoRideJoinWindow ? RideVerdict.TooLate : RideVerdict.Rideable;
        }

        if (reachability == Reachability.CrossDataCenter && lead.TotalSeconds < Math.Max(0, configuration.MinimumLeadSeconds))
        {
            return RideVerdict.TooSoon;
        }

        return RideVerdict.Rideable;
    }

    private static bool DataCenterAllowed(Configuration configuration, in WorldInfo world)
    {
        if (configuration.AllowedDataCenters.Length == 0)
        {
            return Worlds.TryHome(out var home) && home.DataCenterId == world.DataCenterId;
        }

        return configuration.ListsDataCenter(world.DataCenterName);
    }

    private static bool InDuty()
        => Svc.Condition[ConditionFlag.BoundByDuty]
        || Svc.Condition[ConditionFlag.BoundByDuty56]
        || Svc.Condition[ConditionFlag.InDutyQueue];

    public static string Explain(RideVerdict verdict) => verdict switch
    {
        RideVerdict.Rideable => "rideable",
        RideVerdict.NotEnabledGroup => "its expansion group is off in the Feed settings",
        RideVerdict.NotAllowedDataCenter => "its data center is not an allowed one",
        RideVerdict.OutOfRegion => "it runs in another region",
        RideVerdict.CrossDataCenterOff => "rides to other data centers are off",
        RideVerdict.TooSoon => "it starts sooner than the lead time a data center transfer needs",
        RideVerdict.TooLate => "it started too long ago to be worth the trip",
        RideVerdict.InDuty => "the character is in a duty or a duty queue",
        RideVerdict.LifestreamBusy => "Lifestream is busy",
        RideVerdict.RideRunning => "a ride is already running",
        RideVerdict.Snoozed => "auto-ride is snoozed",
        _ => "its world is unknown",
    };
}
