using AutoHuntTrain.Core.Changelog;
using Dalamud.Configuration;
using ECommons.Throttlers;
using Newtonsoft.Json;

namespace AutoHuntTrain;

[Serializable]
public sealed partial class Configuration : IPluginConfiguration
{
    // 1: the expansion toggles became the auto-join switches and the AutoRide master switch was retired.
    public const int CurrentVersion = 1;

    [JsonIgnore]
    private bool savePending;

    public int Version { get; set; }

    public bool AutoShowOnLogin { get; set; } = false;

    public string Language { get; set; } = "";

    // The bundled hunt preset is rewritten in the combat plugin only while this trails its revision, so edits to it survive otherwise.
    public int BundledCombatPresetRevision { get; set; }

    // Never fires on a manual Stop or a fault.
    public AfterRunAction AfterRun { get; set; } = AfterRunAction.StayLoggedIn;

    public bool AutoPauseInContent { get; set; } = true;

    public string LastSeenChangelogVersion { get; set; } = string.Empty;

    [JsonIgnore]
    public bool HasUnseenChangelog => !string.Equals(LastSeenChangelogVersion, ChangelogData.LatestVersion, StringComparison.Ordinal);

    public static Configuration CreateFresh() => new()
    {
        Version = CurrentVersion,
        TrainListView = TrainListView.Everywhere,
    };

    // True when the loaded config was older and changed, so the caller saves it once.
    public bool Migrate()
    {
        if (Version >= CurrentVersion)
        {
            return false;
        }

        if (Version < 1)
        {
            MigrateToAutoJoin();
        }

        Version = CurrentVersion;
        return true;
    }

    // The expansion toggles used to filter what auto-ride took behind a master switch; now each one auto-joins on its
    // own, so a player who had auto-ride off must not start getting auto-joins.
    private void MigrateToAutoJoin()
    {
        if (!AutoRide)
        {
            DisableAutoJoin();
        }

        AutoRide = false;
        Core.RunLog.Info($"Configuration migrated to version 1: auto-join is {(IsAutoJoinActive() ? "on for the expansions that were ridden" : "off")}.");
    }

    public void MarkChangelogSeen()
    {
        if (!HasUnseenChangelog)
        {
            return;
        }

        LastSeenChangelogVersion = ChangelogData.LatestVersion;
        Save();
    }

    public void Save()
    {
        savePending = false;
        Plugin.PluginInterface.SavePluginConfig(this);
    }

    // A slider or a text box changes on every frame it is edited, so the throttle drops most calls; the change stays
    // pending, and FlushPendingSave writes the last one once the throttle window has passed.
    public void SaveDebounced()
    {
        savePending = true;
        FlushPendingSave();
    }

    public void FlushPendingSave()
    {
        if (savePending && EzThrottler.Throttle(Core.AhtConstants.ThrottleKeys.Save, Core.AhtConstants.SaveThrottleMs))
        {
            Save();
        }
    }

    public void SaveIfPending()
    {
        if (savePending)
        {
            Save();
        }
    }
}
