using AutoHuntTrain.Core.Feed;
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

        SettingsRow.Draw(Loc.T(L.Notify.ChatAlert),
            Loc.T(L.Notify.ChatAlertHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.ChatAlert, value => configuration.ChatAlert = value, "##aht_notify_chat"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Notify.ChatSound),
            Loc.T(L.Notify.ChatSoundHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.ChatAlertSound, value => configuration.ChatAlertSound = value, "##aht_notify_chat_sound"),
            SettingsRow.ToggleHeight);

        using (var sound = Motion.PushSection("##aht_notify_chat_sound_effect", configuration.ChatAlertSound))
        {
            if (sound is not null)
            {
                SettingsRow.Draw(Loc.T(L.Notify.ChatSoundEffect),
                    Loc.T(L.Notify.ChatSoundEffectHelp),
                    Stepper.DefaultWidth,
                    () => DrawSoundStepper(configuration));
            }
        }

        SettingsRow.DrawBlock(Loc.T(L.Notify.Test), Loc.T(L.Notify.TestHelp), DrawTestButton);

        SettingsRow.Note(Loc.T(L.Notify.HuntAlertsNote));
    }

    private static void DrawSoundStepper(Configuration configuration)
    {
        var effect = configuration.ChatAlertSoundEffect;
        if (!Stepper.Draw("##aht_notify_sound_effect", ref effect, 1, TrainChatAlert.MinimumSoundEffect, TrainChatAlert.MaximumSoundEffect, Loc.T(L.Notify.SoundFormat)))
        {
            return;
        }

        configuration.ChatAlertSoundEffect = effect;
        configuration.SaveDebounced();
        TrainChatAlert.PlaySound(effect);
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
