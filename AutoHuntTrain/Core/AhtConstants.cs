namespace AutoHuntTrain.Core;

internal static class AhtConstants
{
    public const string PrimaryCommand = "/aht";
    public const string AliasCommand = "/hunttrain";

    public const string LogPrefix = "[AHT]";

    public const int SaveThrottleMs = 500;

    internal static class ThrottleKeys
    {
        public const string Save = "AutoHuntTrain.Save";
    }
}
