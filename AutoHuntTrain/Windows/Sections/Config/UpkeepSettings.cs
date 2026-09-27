using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class UpkeepSettings
{
    private const int QuietSecondsMax = 120;
    private const int QuietSecondsStep = 5;

    public static void Draw(Configuration configuration)
    {
        DrawTimingGroup(configuration);
        RepairSettings.Draw(configuration);
        ConsumableSettings.Draw(configuration);
    }

    private static void DrawTimingGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Safety.UpkeepTiming));

        SettingsRow.Draw(Loc.T(L.Safety.UpkeepQuiet),
            Loc.T(L.Safety.UpkeepQuietHelp),
            Stepper.DefaultWidth,
            () => DrawQuietStepper(configuration));
    }

    private static void DrawQuietStepper(Configuration configuration)
    {
        var seconds = configuration.UpkeepQuietSeconds;
        if (!Stepper.Draw("##aht_upkeep_quiet", ref seconds, QuietSecondsStep, Configuration.UpkeepQuietSecondsMin, QuietSecondsMax, Loc.T(L.Safety.SecondsFormat)))
        {
            return;
        }

        configuration.UpkeepQuietSeconds = seconds;
        configuration.SaveDebounced();
    }
}
