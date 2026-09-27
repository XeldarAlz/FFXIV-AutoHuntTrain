using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Travel;
using AutoHuntTrain.Windows.Components;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface.Utility.Raii;

namespace AutoHuntTrain.Windows.Sections.Config;

internal static class FeedSettings
{
    private const int LeadSecondsMin = 0;
    private const int LeadSecondsMax = 900;
    private const int LeadSecondsStep = 30;
    private const int SnoozeMinutesMin = 5;
    private const int SnoozeMinutesMax = 180;
    private const int SnoozeMinutesStep = 5;
    private const string DataCenterToggleId = "##aht_feed_datacenter";

    private static readonly GroupRow[] groupRows =
    [
        new(ExpansionGroup.Centurio, L.Feed.AutoJoinCenturio, "##aht_feed_centurio"),
        new(ExpansionGroup.Shadowbringers, L.Feed.AutoJoinShadowbringers, "##aht_feed_shb"),
        new(ExpansionGroup.Endwalker, L.Feed.AutoJoinEndwalker, "##aht_feed_ew"),
        new(ExpansionGroup.Dawntrail, L.Feed.AutoJoinDawntrail, "##aht_feed_dt"),
    ];

    public static void Draw(Configuration configuration)
    {
        using var group = SettingsGroup.Begin(Loc.T(L.Feed.AutoJoinGroup));

        for (var index = 0; index < groupRows.Length; index++)
        {
            var row = groupRows[index];
            SettingsRow.Draw(Loc.T(row.Label),
                Loc.T(L.Feed.AutoJoinHelp),
                SettingsControls.ToggleWidth,
                () => DrawGroupToggle(configuration, row),
                SettingsRow.ToggleHeight);
        }

        SettingsRow.DrawBlock(Loc.T(L.Feed.DataCenters),
            Loc.T(L.Feed.DataCentersHelp),
            () => DrawDataCenterList(configuration));

        SettingsRow.Draw(Loc.T(L.Feed.CrossDataCenter),
            Loc.T(L.Feed.CrossDataCenterHelp),
            SettingsControls.ToggleWidth,
            () => SettingsControls.DrawToggle(configuration, () => configuration.AllowCrossDataCenterRides, value => configuration.AllowCrossDataCenterRides = value, "##aht_feed_crossdc"),
            SettingsRow.ToggleHeight);

        SettingsRow.Draw(Loc.T(L.Feed.MinimumLead),
            Loc.T(L.Feed.MinimumLeadHelp),
            Stepper.DefaultWidth,
            () => DrawLeadStepper(configuration));

        SettingsRow.Draw(Loc.T(L.Feed.SnoozeLength),
            Loc.T(L.Feed.SnoozeLengthHelp),
            Stepper.DefaultWidth,
            () => DrawSnoozeStepper(configuration));
    }

    private static void DrawGroupToggle(Configuration configuration, in GroupRow row)
    {
        var enabled = configuration.IsGroupEnabled(row.Group);
        if (!ToggleSwitch.Draw(row.Id, ref enabled))
        {
            return;
        }

        configuration.SetGroupEnabled(row.Group, enabled);
        configuration.SaveDebounced();
    }

    // With nothing ticked the home data center counts as ticked; the first tick elsewhere keeps it that way, so a
    // player adding a data center does not silently lose their own.
    private static void DrawDataCenterList(Configuration configuration)
    {
        if (!Worlds.TryHome(out var home))
        {
            SettingsRow.Note(Loc.T(L.Feed.NoHomeRegion));
            return;
        }

        var dataCenters = Worlds.DataCentersIn(home.Region);
        var noneListed = configuration.AllowedDataCenters.Length == 0;
        for (var index = 0; index < dataCenters.Length; index++)
        {
            var dataCenter = dataCenters[index];
            var isHome = dataCenter.Id == home.DataCenterId;
            var selected = configuration.ListsDataCenter(dataCenter.Name) || (noneListed && isHome);
            ImGui.PushID((int)dataCenter.Id);
            var changed = ToggleSwitch.Draw(DataCenterToggleId, ref selected);
            ImGui.PopID();
            if (changed)
            {
                if (selected && noneListed && !isHome)
                {
                    configuration.SetDataCenterAllowed(home.DataCenterName, true);
                }

                configuration.SetDataCenterAllowed(dataCenter.Name, selected);
                configuration.SaveDebounced();
            }

            ImGui.SameLine();
            ImGui.AlignTextToFramePadding();
            using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextStrong))
            {
                ImGui.TextUnformatted(dataCenter.Name);
            }

            if (!isHome)
            {
                continue;
            }

            ImGui.SameLine();
            using (ImRaii.PushColor(ImGuiCol.Text, Styling.TextDim))
            {
                ImGui.TextUnformatted(Loc.T(L.Feed.HomeMarker));
            }
        }

        ImGui.Spacing();
    }

    private static void DrawLeadStepper(Configuration configuration)
    {
        var seconds = configuration.MinimumLeadSeconds;
        if (!Stepper.Draw("##aht_feed_lead", ref seconds, LeadSecondsStep, LeadSecondsMin, LeadSecondsMax, Loc.T(L.Safety.SecondsFormat)))
        {
            return;
        }

        configuration.MinimumLeadSeconds = seconds;
        configuration.SaveDebounced();
    }

    private static void DrawSnoozeStepper(Configuration configuration)
    {
        var minutes = configuration.SnoozeMinutes;
        if (!Stepper.Draw("##aht_feed_snooze", ref minutes, SnoozeMinutesStep, SnoozeMinutesMin, SnoozeMinutesMax, Loc.T(L.Safety.MinutesFormat)))
        {
            return;
        }

        configuration.SnoozeMinutes = minutes;
        configuration.SaveDebounced();
    }

    private readonly record struct GroupRow(ExpansionGroup Group, LocString Label, string Id);
}
