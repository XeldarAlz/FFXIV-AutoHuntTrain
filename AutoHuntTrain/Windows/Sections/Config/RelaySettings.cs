using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class RelaySettings
{
    private const float ChannelComboWidth = 220f;

    public static void Draw(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Details.SettingsGroup));

        SettingsRow.Draw(Loc.T(L.Details.RelayChannel),
            Loc.T(L.Details.RelayChannelHelp),
            ChannelComboWidth,
            () => DrawChannelCombo(configuration));

        SettingsRow.Draw(Loc.T(L.Details.RelayFlag),
            Loc.T(L.Details.RelayFlagHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.RelayWithFlag, value => configuration.RelayWithFlag = value, "##aht_relay_flag"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawChannelCombo(Configuration configuration)
    {
        var selected = (int)configuration.RelayChannel;
        if (!SettingsControls.DrawPlainCombo("##aht_relay_channel", ref selected, RelayChannelLabels.All(), ChannelComboWidth))
        {
            return;
        }

        configuration.RelayChannel = (RelayChannel)selected;
        configuration.SaveDebounced();
    }
}
