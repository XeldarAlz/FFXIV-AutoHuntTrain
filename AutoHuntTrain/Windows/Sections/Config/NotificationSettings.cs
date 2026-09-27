using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class NotificationSettings
{
    public static void Draw(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Notify.SettingsGroup));

        SettingsRow.Draw(Loc.T(L.Notify.OpenWindow),
            Loc.T(L.Notify.OpenWindowHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.NotifyOpenWindow, value => configuration.NotifyOpenWindow = value, "##aht_notify_openwindow"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Notify.FlashTaskbar),
            Loc.T(L.Notify.FlashTaskbarHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.NotifyFlashTaskbar, value => configuration.NotifyFlashTaskbar = value, "##aht_notify_flash"),
            SettingsRow.ToggleHeight);

        SettingsRow.DrawBlock(Loc.T(L.Notify.Test), Loc.T(L.Notify.TestHelp), DrawTestButton);

        SettingsRow.Note(Loc.T(L.Notify.HuntAlertsNote));
    }

    private static void DrawTestButton()
    {
        bool clicked;
        ImGui.PushID("##aht_notify_test");
        using (ImRaii.PushColor(ImGuiCol.Text, Styling.AccentAmber))
        {
            clicked = ImGui.SmallButton(Loc.T(L.Notify.TestButton));
        }

        ImGui.PopID();
        if (clicked)
        {
            Plugin.Instance.Notifier.Test();
        }
    }
}
