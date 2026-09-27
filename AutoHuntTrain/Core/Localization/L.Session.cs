namespace AutoHuntTrain.Core.Localization;

internal static partial class L
{
    internal static class Session
    {
        public static readonly LocString AutoResume = new("session.autoResume", "Auto-resume on fault");
        public static readonly LocString AutoResumeHelp = new("session.autoResumeHelp", "If the ride hits an unexpected error and stops, restart it automatically (up to 3 times in 5 minutes) instead of ending it. The restarted ride takes the conductor's latest flag and never follows one it already finished. Turn it off if you would rather have errors end the ride.");
    }
}
