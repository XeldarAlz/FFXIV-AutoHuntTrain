using AutoHuntTrain.Core.Game;
using Dalamud.Game.ClientState.Conditions;
using ECommons.DalamudServices;

namespace AutoHuntTrain.Core.Feed;

// Tells a player who is not looking at the plugin that a train they could ride was announced: the window opens on
// the Train page and the taskbar flashes while the game is in the background. HuntAlerts already posts its own chat
// alert, sound and banner, so nothing here repeats those.
internal sealed class TrainNotifier : IDisposable
{
    private const int RateLimitMs = 20_000;
    private const int OpenWindowPatienceMs = 30_000;

    private readonly FeedListener feed;
    private readonly Action<Announcement> onAnnounced;
    private long nextNotifyAtMs;
    private long openWindowDeadlineMs;
    private bool openWindowPending;

    public TrainNotifier(FeedListener feed)
    {
        this.feed = feed;
        onAnnounced = OnAnnounced;
        feed.Announced += onAnnounced;
    }

    public void Dispose() => feed.Announced -= onAnnounced;

    // The window never takes the keyboard from a text box; it waits for the player to finish typing, within reason.
    public void Tick()
    {
        if (!openWindowPending)
        {
            return;
        }

        if (Environment.TickCount64 > openWindowDeadlineMs)
        {
            openWindowPending = false;
            RunLog.Debug("Notify: a text box kept the keyboard for 30 seconds; the Train page is not opened");
            return;
        }

        if (TextInputFocus.Active())
        {
            return;
        }

        openWindowPending = false;
        Plugin.Instance.ShowTrainPage();
    }

    public void Test()
    {
        var configuration = Plugin.Instance.Configuration;
        RunLog.Info("Notify: testing the notifications");
        if (configuration.NotifyOpenWindow)
        {
            RequestOpenWindow();
        }

        if (configuration.NotifyFlashTaskbar)
        {
            WindowFlash.FlashBriefly();
        }
    }

    private void OnAnnounced(Announcement announcement)
    {
        var configuration = Plugin.Instance.Configuration;
        if (!configuration.NotifyOpenWindow && !configuration.NotifyFlashTaskbar)
        {
            return;
        }

        var group = ExpansionGroups.Name(announcement.Group);
        if (!ShouldNotify(configuration, announcement, out var reason))
        {
            RunLog.Debug($"Notify: the {group} train on {announcement.World.Name} is not notified, {reason}");
            return;
        }

        var now = Environment.TickCount64;
        if (now < nextNotifyAtMs)
        {
            RunLog.Debug($"Notify: the {group} train on {announcement.World.Name} is not notified, another notification went out less than 20 seconds ago");
            return;
        }

        nextNotifyAtMs = now + RateLimitMs;
        RunLog.Info($"Notify: the {group} train on {announcement.World.Name} could be ridden");
        if (configuration.NotifyOpenWindow)
        {
            RequestOpenWindow();
        }

        if (configuration.NotifyFlashTaskbar && WindowFlash.GameInBackground())
        {
            WindowFlash.FlashUntilFocused();
        }
    }

    // A refusal the player can lift on the spot, by allowing other data centers or waiting out Lifestream, still
    // notifies; every other refusal means the train is not one for them.
    private static bool ShouldNotify(Configuration configuration, in Announcement announcement, out string reason)
    {
        var nowUtc = DateTime.UtcNow;
        if (configuration.IsSnoozed(nowUtc))
        {
            reason = "auto-ride is snoozed";
            return false;
        }

        if (Plugin.Instance.Controller.Running)
        {
            reason = "a ride is running";
            return false;
        }

        if (InDutyOrCutscene())
        {
            reason = "the character is in a duty or a cutscene";
            return false;
        }

        var verdict = RideRules.Evaluate(announcement, nowUtc, forAutoRide: false, out _);
        if (verdict is RideVerdict.Rideable or RideVerdict.CrossDataCenterOff or RideVerdict.LifestreamBusy)
        {
            reason = string.Empty;
            return true;
        }

        reason = RideRules.Explain(verdict);
        return false;
    }

    private void RequestOpenWindow()
    {
        openWindowPending = true;
        openWindowDeadlineMs = Environment.TickCount64 + OpenWindowPatienceMs;
    }

    private static bool InDutyOrCutscene()
        => Svc.Condition[ConditionFlag.OccupiedInCutSceneEvent]
        || Svc.Condition[ConditionFlag.WatchingCutscene]
        || Svc.Condition[ConditionFlag.WatchingCutscene78]
        || Svc.Condition[ConditionFlag.BoundByDuty]
        || Svc.Condition[ConditionFlag.BoundByDuty56]
        || Svc.Condition[ConditionFlag.BoundByDuty95];
}
