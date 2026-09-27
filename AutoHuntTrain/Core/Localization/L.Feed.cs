namespace AutoHuntTrain.Core.Localization;

internal static partial class L
{
    internal static class Feed
    {
        public static readonly LocString HeadlineHuntAlerts = new("feed.headline.huntAlerts", "Feed: HuntAlerts");
        public static readonly LocString HeadlineDetail = new("feed.headline.detail", "{0}  ·  {1}");
        public static readonly LocString AutoRideOn = new("feed.autoRide.on", "Auto-ride on");
        public static readonly LocString AutoRideOff = new("feed.autoRide.off", "Auto-ride off");
        public static readonly LocString SnoozedUntil = new("feed.autoRide.snoozedUntil", "{0}  ·  snoozed until {1}");

        public static readonly LocString BadgeCenturio = new("feed.badge.centurio", "Centurio");
        public static readonly LocString BadgeShadowbringers = new("feed.badge.shb", "ShB");
        public static readonly LocString BadgeEndwalker = new("feed.badge.ew", "EW");
        public static readonly LocString BadgeDawntrail = new("feed.badge.dt", "DT");
        public static readonly LocString WorldLine = new("feed.worldLine", "{0}  ·  {1}");
        public static readonly LocPlural InMinutes = new("feed.time.inMinutes", "in {0} minute", "in {0} minutes");
        public static readonly LocPlural StartedAgo = new("feed.time.startedAgo", "started {0} minute ago", "started {0} minutes ago");
        public static readonly LocString StartingNow = new("feed.time.startingNow", "starting now");
        public static readonly LocString ReachSameWorld = new("feed.reach.sameWorld", "This world");
        public static readonly LocString ReachSameDataCenter = new("feed.reach.sameDataCenter", "Same data center");
        public static readonly LocString ReachCrossDataCenter = new("feed.reach.crossDataCenter", "Other data center");
        public static readonly LocString ReachOutOfRegion = new("feed.reach.outOfRegion", "Other region");
        public static readonly LocString Ride = new("feed.ride", "Ride");
        public static readonly LocString RideHint = new("feed.rideHint", "Travel to the start of this train and follow it");

        public static readonly LocString VerdictNotEnabledGroup = new("feed.verdict.notEnabledGroup", "This expansion group is off in the Feed settings");
        public static readonly LocString VerdictNotAllowedDataCenter = new("feed.verdict.notAllowedDataCenter", "{0} is not an allowed data center");
        public static readonly LocString VerdictOutOfRegion = new("feed.verdict.outOfRegion", "This train runs in another region");
        public static readonly LocString VerdictCrossDataCenterOff = new("feed.verdict.crossDataCenterOff", "Rides to other data centers are off in the Feed settings");
        public static readonly LocString VerdictTooSoon = new("feed.verdict.tooSoon", "Starts sooner than the lead time a data center transfer needs");
        public static readonly LocString VerdictTooLate = new("feed.verdict.tooLate", "Started too long ago to be worth the trip");
        public static readonly LocString VerdictInDuty = new("feed.verdict.inDuty", "Leave the duty or its queue first");
        public static readonly LocString VerdictLifestreamBusy = new("feed.verdict.lifestreamBusy", "Lifestream is busy with something else");
        public static readonly LocString VerdictRideRunning = new("feed.verdict.rideRunning", "A ride is already running");
        public static readonly LocString VerdictSnoozed = new("feed.verdict.snoozed", "Auto-ride is snoozed");
        public static readonly LocString VerdictFeedWorldUnknown = new("feed.verdict.feedWorldUnknown", "The announced world is not one this client knows");

        public static readonly LocString PhaseJourney = new("feed.phase.journey", "Travelling to the train");
        public static readonly LocString ConductorNamed = new("feed.conductor.named", "Conductor: {0}");
        public static readonly LocString ConductorAnnounced = new("feed.conductor.announced", "Conductor: {0}, named in the announcement");
        public static readonly LocString ConductorPicked = new("feed.conductor.picked", "Conductor: {0}, picked from the first flag");
        public static readonly LocString ConductorPendingIn = new("feed.conductor.pendingIn", "The first player to post a flag in {0} becomes the conductor");
        public static readonly LocString ConductorPending = new("feed.conductor.pending", "The first player to post a flag becomes the conductor");

        public static readonly LocString SettingsGroup = new("feed.settings.group", "Feed");
        public static readonly LocString RideCenturio = new("feed.settings.rideCenturio", "Ride Centurio trains");
        public static readonly LocString RideShadowbringers = new("feed.settings.rideShb", "Ride Shadowbringers trains");
        public static readonly LocString RideEndwalker = new("feed.settings.rideEw", "Ride Endwalker trains");
        public static readonly LocString RideDawntrail = new("feed.settings.rideDt", "Ride Dawntrail trains");
        public static readonly LocString RideGroupHelp = new("feed.settings.rideGroupHelp", "Announced trains of this expansion group are listed and ridden. Off hides them from the Upcoming trains list and from auto-ride.");
        public static readonly LocString DataCenters = new("feed.settings.dataCenters", "Allowed data centers");
        public static readonly LocString DataCentersHelp = new("feed.settings.dataCentersHelp", "Trains on the ticked data centers are ridden. With none ticked, only trains on your home data center are.");
        public static readonly LocString HomeMarker = new("feed.settings.home", "(home)");
        public static readonly LocString NoHomeRegion = new("feed.settings.noHomeRegion", "Log in to list the data centers of your region.");
        public static readonly LocString CrossDataCenter = new("feed.settings.crossDataCenter", "Rides to other data centers");
        public static readonly LocString CrossDataCenterHelp = new("feed.settings.crossDataCenterHelp", "Allow a ride that needs a data center transfer. The transfer logs the character out and back in, and Lifestream must have data center travel enabled.");
        public static readonly LocString MinimumLead = new("feed.settings.minimumLead", "Lead time for another data center");
        public static readonly LocString MinimumLeadHelp = new("feed.settings.minimumLeadHelp", "Auto-ride skips a train on another data center whose start is closer than this, because a data center transfer takes minutes plus a queue. Trains on your own data center are ridden as soon as they are announced, and a train that already started is still joined for ten minutes after its start.");
        public static readonly LocString AutoRide = new("feed.settings.autoRide", "Auto-ride");
        public static readonly LocString AutoRideHelp = new("feed.settings.autoRideHelp", "Ride the soonest announced train that passes every rule as soon as one does, with no click from you. Off leaves the Ride buttons to you.");
        public static readonly LocString SnoozeLength = new("feed.settings.snoozeLength", "Snooze length");
        public static readonly LocString SnoozeLengthHelp = new("feed.settings.snoozeLengthHelp", "How long /aht snooze suspends auto-ride. /aht snooze off lifts it early.");
    }
}
