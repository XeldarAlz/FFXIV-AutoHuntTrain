using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class AfterRideSettings
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
        using var group = SettingsGroup.Begin(Loc.T(L.Train.WhenDone));

        SettingsRow.Draw(Loc.T(L.Train.ReturnHome),
            Loc.T(L.Train.ReturnHomeHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.ReturnHomeAfterRide, value => configuration.ReturnHomeAfterRide = value, "##aht_general_returnhome"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Train.StayForNext),
            Loc.T(L.Train.StayForNextHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.StayForNextTrain, value => configuration.StayForNextTrain = value, "##aht_general_stayfornext"),
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
