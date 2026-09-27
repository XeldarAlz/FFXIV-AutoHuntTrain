using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class PartySettings
{
    private const float TextInputWidth = 360f;
    // The chat box refuses a line longer than 500 bytes, and "/shout " takes a few of them.
    private const int TextMaxLength = 480;

    public static void Draw(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Safety.PartyGroup));

        SettingsRow.Draw(Loc.T(L.Safety.AcceptInvites),
            Loc.T(L.Safety.AcceptInvitesHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.AcceptPartyInvites, value => configuration.AcceptPartyInvites = value, "##aht_party_accept"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Safety.LookingForGroup),
            Loc.T(L.Safety.LookingForGroupHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.PostLookingForGroup, value => configuration.PostLookingForGroup = value, "##aht_party_lfg"),
            SettingsRow.ToggleHeight);

        using (var text = Motion.PushSection("##aht_party_lfg_text", configuration.PostLookingForGroup))
        {
            if (text is not null)
            {
                SettingsRow.DrawBlock(Loc.T(L.Safety.LookingForGroupText),
                    Loc.T(L.Safety.LookingForGroupTextHelp),
                    () => DrawTextInput(configuration));
            }
        }

        SettingsRow.Draw(Loc.T(L.Safety.LeaveParty),
            Loc.T(L.Safety.LeavePartyHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.LeavePartyAfterRide, value => configuration.LeavePartyAfterRide = value, "##aht_party_leave"),
            SettingsRow.ToggleHeight);
    }

    private static void DrawTextInput(Configuration configuration)
    {
        var text = configuration.LookingForGroupText;
        bool edited;
        ImGui.SetNextItemWidth(TextInputWidth * ImGuiHelpers.GlobalScale);
        using (SettingsControls.PushFrameColors())
        {
            edited = ImGui.InputText("##aht_party_lfg_input", ref text, TextMaxLength);
        }

        if (!edited)
        {
            return;
        }

        configuration.LookingForGroupText = text;
        configuration.SaveDebounced();
    }
}
