namespace AutoHuntTrain.Core.Localization;

internal static partial class L
{
    internal static class Ride
    {
        public static readonly LocString TitlePickConductor = new("ride.title.pickConductor", "Pick a conductor");
        public static readonly LocString DetailPickConductor = new("ride.detail.pickConductor", "Choose whose flags to follow on the Ride card below, or type /aht conductor First Last@World.");
        public static readonly LocString StatusPickConductor = new("ride.status.pickConductor", "No conductor");
        public static readonly LocString ReasonPickConductor = new("ride.reason.pickConductor", "pick a conductor");

        public static readonly LocString CardConductor = new("ride.card.conductor", "Conductor");
        public static readonly LocString CardPickRecent = new("ride.card.pickRecent", "Pick a recent poster");
        public static readonly LocString CardTypeHint = new("ride.card.typeHint", "First Last@World");
        public static readonly LocString CardClear = new("ride.card.clear", "Clear the conductor");
        public static readonly LocString CardFollowing = new("ride.card.following", "Following {0}");
        public static readonly LocString CardNone = new("ride.card.none", "No conductor picked yet. Recent flag posters in Shout, Yell and Say on this world show up in the list.");
        public static readonly LocString CardLatestFlag = new("ride.card.latestFlag", "Latest flag: {0}  ·  {1}");
        public static readonly LocString CardNoFlagYet = new("ride.card.noFlagYet", "No flag from them yet.");
        public static readonly LocString SecondsAgo = new("ride.time.secondsAgo", "{0}s ago");

        public static readonly LocString FlagLine = new("ride.flag.line", "{0}  ·  ({1:F1}, {2:F1})");
        public static readonly LocString FlagLineInstance = new("ride.flag.lineInstance", "{0}  ·  ({1:F1}, {2:F1})  ·  i{3}");
        public static readonly LocString PhaseWaiting = new("ride.phase.waiting", "Waiting for a flag");
        public static readonly LocString PhaseTravelling = new("ride.phase.travelling", "Riding to the flag");
        public static readonly LocString PhaseAtFlag = new("ride.phase.atFlag", "At the flag");
        public static readonly LocString PhaseWaitingForMark = new("ride.phase.waitingForMark", "Waiting for the mark");
        public static readonly LocPlural FlagsFollowed = new("ride.flagsFollowed", "{0} flag followed", "{0} flags followed");
        public static readonly LocString ProgressLine = new("ride.progressLine", "{0}  ·  {1}");
        public static readonly LocString CreditedOf = new("ride.creditedOf", "{0} of {1} credited");

        public static readonly LocString SettingsGroup = new("ride.settings.group", "Conductor");
        public static readonly LocString ListenShout = new("ride.settings.shout", "Listen to Shout");
        public static readonly LocString ListenShoutHelp = new("ride.settings.shoutHelp", "Follow flags the conductor posts in Shout, the channel most trains run on.");
        public static readonly LocString ListenYell = new("ride.settings.yell", "Listen to Yell");
        public static readonly LocString ListenYellHelp = new("ride.settings.yellHelp", "Follow flags the conductor posts in Yell, which some trains use to spare the Shout channel.");
        public static readonly LocString ListenSay = new("ride.settings.say", "Listen to Say");
        public static readonly LocString ListenSayHelp = new("ride.settings.sayHelp", "Follow flags the conductor posts in Say. Handy when you stand next to the conductor, or when you test the ride with your own flags.");
        public static readonly LocString LateJoin = new("ride.settings.lateJoin", "Late-join limit");
        public static readonly LocString LateJoinHelp = new("ride.settings.lateJoinHelp", "When a ride starts, the conductor's last flag is still followed if it is at most this old. An older flag is skipped and the ride waits for the next one.");
        public static readonly LocString IdleLimit = new("ride.settings.idle", "Idle limit");
        public static readonly LocString IdleLimitHelp = new("ride.settings.idleHelp", "The ride ends on its own when no flag from the conductor arrives for this long.");

        public static readonly LocString EngagementGroup = new("ride.settings.engagement", "Engagement");
        public static readonly LocString WaitForPull = new("ride.settings.waitForPull", "Wait for the pull");
        public static readonly LocString WaitForPullHelp = new("ride.settings.waitForPullHelp", "Hold at the flag until someone else has pulled the mark, then join the fight. Off attacks the mark as soon as it is in sight, which makes you the one who pulls it.");
        public static readonly LocString PullWait = new("ride.settings.pullWait", "Pull wait");
        public static readonly LocString PullWaitHelp = new("ride.settings.pullWaitHelp", "How long to wait at a flag for the mark to show up and be pulled. When the time runs out, the ride gives the mark up and waits for the next flag.");
        public static readonly LocString EndWhenAllCredited = new("ride.settings.endWhenAllCredited", "End when every mark is credited");
        public static readonly LocString EndWhenAllCreditedHelp = new("ride.settings.endWhenAllCreditedHelp", "End the ride on its own once as many marks are credited as the expansion has A ranks: 17 in A Realm Reborn, 12 in every later expansion.");
    }
}
