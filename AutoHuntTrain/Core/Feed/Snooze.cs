using ECommons.DalamudServices;
using System.Globalization;

namespace AutoHuntTrain.Core.Feed;

// Suspends auto-ride for a while: the configured snooze length, or the minutes given with the command.
internal static class Snooze
{
    private const string OffArgument = "off";
    private const int MaxMinutes = 720;
    private const string Usage = AhtConstants.LogPrefix + " Usage: /aht snooze [<minutes>], or /aht snooze off.";
    private const string ClockFormat = "HH:mm";

    public static void HandleCommand(string arguments)
    {
        if (arguments.Equals(OffArgument, StringComparison.OrdinalIgnoreCase))
        {
            Lift();
            return;
        }

        var minutes = Plugin.Instance.Configuration.SnoozeMinutes;
        if (arguments.Length > 0 && (!int.TryParse(arguments, NumberStyles.None, CultureInfo.InvariantCulture, out minutes) || minutes <= 0 || minutes > MaxMinutes))
        {
            Svc.Chat.PrintError(Usage);
            return;
        }

        Set(minutes);
    }

    public static void Set(int minutes)
    {
        var configuration = Plugin.Instance.Configuration;
        var until = DateTime.UtcNow.AddMinutes(minutes);
        configuration.SnoozeUntilUtc = until;
        configuration.SaveDebounced();
        RunLog.Info($"Auto-ride snoozed for {minutes} minutes, until {until:HH:mm}Z.");
        var note = configuration.AutoRide ? string.Empty : " Auto-ride is off, so the snooze only matters once it is on.";
        Svc.Chat.Print($"{AhtConstants.LogPrefix} Auto-ride snoozed for {minutes} minutes, until {until.ToLocalTime().ToString(ClockFormat, CultureInfo.InvariantCulture)}.{note}");
    }

    public static void Lift()
    {
        var configuration = Plugin.Instance.Configuration;
        var wasSnoozed = configuration.IsSnoozed(DateTime.UtcNow);
        if (configuration.SnoozeUntilUtc is not null)
        {
            configuration.SnoozeUntilUtc = null;
            configuration.SaveDebounced();
        }

        if (!wasSnoozed)
        {
            Svc.Chat.Print($"{AhtConstants.LogPrefix} Auto-ride is not snoozed.");
            return;
        }

        RunLog.Info("Auto-ride snooze lifted.");
        Svc.Chat.Print($"{AhtConstants.LogPrefix} Auto-ride snooze lifted.");
    }
}
