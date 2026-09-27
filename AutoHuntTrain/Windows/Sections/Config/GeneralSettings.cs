using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class GeneralSettings
{
    private static readonly AfterRunAction[] afterRunOrder =
        [AfterRunAction.StayLoggedIn, AfterRunAction.ReturnToInn, AfterRunAction.Logout, AfterRunAction.CloseGame];

    // Ordered like afterRunOrder, because the picked index reads both.
    private static readonly SettingsControls.Choices.Choice[] afterRunChoices =
    [
        new(L.Train.AfterStayName, L.Train.AfterStayDetail),
        new(L.Train.AfterInnName, L.Train.AfterInnDetail),
        new(L.Train.AfterLogoutName, L.Train.AfterLogoutDetail),
        new(L.Train.AfterCloseName, L.Train.AfterCloseDetail),
    ];

    public static void Draw(Configuration configuration)
    {
        DrawLanguageGroup(configuration);
        DrawWindowGroup(configuration);
        DrawBehaviorGroup(configuration);
        ConductorSettings.Draw(configuration);
        DrawAfterRideGroup(configuration);
    }

    private static void DrawLanguageGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Settings.Language));

        SettingsRow.Draw(Loc.T(L.Settings.Language),
            Loc.T(L.Settings.LanguageHelp),
            SettingsControls.RowComboWidth,
            () => SettingsControls.DrawLanguageCombo(configuration));
    }

    private static void DrawWindowGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Settings.GeneralWindow));

        SettingsRow.Draw(Loc.T(L.Settings.OpenOnLogin),
            Loc.T(L.Settings.OpenOnLoginHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.AutoShowOnLogin, value => configuration.AutoShowOnLogin = value, "##aht_general_autoshow"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawBehaviorGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Settings.GeneralBehavior));

        SettingsRow.Draw(Loc.T(L.Settings.AutoPause),
            Loc.T(L.Settings.AutoPauseHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.AutoPauseInContent, value => configuration.AutoPauseInContent = value, "##aht_general_autopause"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Session.AutoResume),
            Loc.T(L.Session.AutoResumeHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.AutoResumeOnFault, value => configuration.AutoResumeOnFault = value, "##aht_general_autoresume"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawAfterRideGroup(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Train.WhenDone));

        SettingsRow.Draw(Loc.T(L.Train.ReturnHome),
            Loc.T(L.Train.ReturnHomeHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.ReturnHomeAfterRide, value => configuration.ReturnHomeAfterRide = value, "##aht_general_returnhome"),
            SettingsRow.ToggleHeight);

        var selected = Math.Max(0, Array.IndexOf(afterRunOrder, configuration.AfterRun));
        SettingsRow.Draw(Loc.T(L.Train.WhenDone),
            Loc.T(L.Train.WhenDoneHelp),
            SettingsControls.RowComboWidth,
            () => SettingsControls.Choices.DrawCombo("##aht_general_afterride", afterRunChoices, selected, choice =>
            {
                configuration.AfterRun = afterRunOrder[choice];
                configuration.SaveDebounced();
            }));
        SettingsRow.Caption(Loc.T(afterRunChoices[selected].Detail));
    }
}
