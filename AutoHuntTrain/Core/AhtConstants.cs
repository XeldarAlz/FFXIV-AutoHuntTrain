namespace AutoHuntTrain.Core;

internal static class AhtConstants
{
    public const string PrimaryCommand = "/aht";
    public const string AliasCommand = "/hunttrain";

    public const string LogPrefix = "[AHT]";

    public const int SaveThrottleMs = 500;

    // Some plugin commands misbehave when several arrive in the same frame, so post-run commands are spaced out.
    public const int PostRunCommandSpacingMs = 500;

    internal static class ThrottleKeys
    {
        public const string Save = "AutoHuntTrain.Save";
    }
}
