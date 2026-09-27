using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class ConductorSettings
{
    private const int LateJoinSecondsMin = 0;
    private const int LateJoinSecondsMax = 600;
    private const int LateJoinSecondsStep = 15;
    private const int IdleMinutesMin = 1;
    private const int IdleMinutesMax = 120;
    private const int IdleMinutesStep = 1;
    private const int QuietMinutesMin = 2;
    private const int QuietMinutesMax = 30;
    private const int QuietMinutesStep = 1;

    public static void Draw(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Ride.SettingsGroup));

        SettingsRow.Draw(Loc.T(L.Ride.ListenShout),
            Loc.T(L.Ride.ListenShoutHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.ListenShout, value => configuration.ListenShout = value, "##aht_conductor_shout"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Ride.ListenYell),
            Loc.T(L.Ride.ListenYellHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.ListenYell, value => configuration.ListenYell = value, "##aht_conductor_yell"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Ride.ListenSay),
            Loc.T(L.Ride.ListenSayHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.ListenSay, value => configuration.ListenSay = value, "##aht_conductor_say"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Ride.LateJoin),
            Loc.T(L.Ride.LateJoinHelp),
            Stepper.DefaultWidth,
            () => DrawLateJoinStepper(configuration));

        SettingsRow.Draw(Loc.T(L.Ride.IdleLimit),
            Loc.T(L.Ride.IdleLimitHelp),
            Stepper.DefaultWidth,
            () => DrawIdleStepper(configuration));

        SettingsRow.Draw(Loc.T(L.Ride.QuietEnd),
            Loc.T(L.Ride.QuietEndHelp),
            Stepper.DefaultWidth,
            () => DrawQuietStepper(configuration));

        SettingsRow.Draw(Loc.T(L.Ride.EndOnPhrase),
            Loc.T(L.Ride.EndOnPhraseHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.EndOnConductorPhrase, value => configuration.EndOnConductorPhrase = value, "##aht_conductor_endphrase"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawLateJoinStepper(Configuration configuration)
    {
        var seconds = configuration.LateJoinLimitSeconds;
        if (!Stepper.Draw("##aht_conductor_latejoin", ref seconds, LateJoinSecondsStep, LateJoinSecondsMin, LateJoinSecondsMax, Loc.T(L.Safety.SecondsFormat)))
        {
            return;
        }

        configuration.LateJoinLimitSeconds = seconds;
        configuration.SaveDebounced();
    }

    private static void DrawIdleStepper(Configuration configuration)
    {
        var minutes = configuration.IdleLimitMinutes;
        if (!Stepper.Draw("##aht_conductor_idle", ref minutes, IdleMinutesStep, IdleMinutesMin, IdleMinutesMax, Loc.T(L.Safety.MinutesFormat)))
        {
            return;
        }

        configuration.IdleLimitMinutes = minutes;
        configuration.SaveDebounced();
    }

    private static void DrawQuietStepper(Configuration configuration)
    {
        var minutes = configuration.QuietEndMinutes;
        if (!Stepper.Draw("##aht_conductor_quiet", ref minutes, QuietMinutesStep, QuietMinutesMin, QuietMinutesMax, Loc.T(L.Safety.MinutesFormat)))
        {
            return;
        }

        configuration.QuietEndMinutes = minutes;
        configuration.SaveDebounced();
    }
}
