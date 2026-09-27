using AutoHuntTrain.Core.External;
using AutoHuntTrain.Core.Localization;
using AutoHuntTrain.Core.Tasks;
using AutoHuntTrain.Core.Train;
using Dalamud.Bindings.ImGui;
using Dalamud.Interface;
using System.Numerics;

namespace AutoHuntTrain.Windows.Sections;

internal static class ReadyState
{
    public enum Kind { SetupNeeded, PickConductor, FeedReady, ChatOnly, Running, Paused }

    public readonly record struct Info(Kind Kind, Vector4 Accent, Vector4 AccentSoft, FontAwesomeIcon Icon, string Title, string Detail);

    private static readonly CachedText[] details = new CachedText[6];

    private static int cachedFrame = -1;
    private static Info cached;

    public static Info Resolve(AutoHuntController controller)
    {
        var frame = ImGui.GetFrameCount();
        if (frame == cachedFrame)
        {
            return cached;
        }

        cached = Compute(controller);
        cachedFrame = frame;
        return cached;
    }

    private static Info Compute(AutoHuntController controller)
    {
        if (controller.Running)
        {
            if (controller.Paused)
            {
                var detail = controller.PauseReason == PauseReason.InContent
                    ? Loc.T(L.Train.DetailPausedInContent)
                    : Loc.T(L.Train.DetailPausedManual);
                return new Info(Kind.Paused, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.Pause, Loc.T(L.Train.TitlePaused), detail);
            }

            return new Info(Kind.Running, Styling.AccentBlue, Styling.AccentBlueSoft, FontAwesomeIcon.Train, Loc.T(L.Train.TitleRiding), ActivityLabel(controller));
        }

        if (!ExternalPlugins.AllRequiredInstalled())
        {
            return new Info(Kind.SetupNeeded, Styling.AccentRose, Styling.AccentRoseSoft, FontAwesomeIcon.ExclamationTriangle,
                Loc.T(L.Train.TitleSetupNeeded), Loc.T(L.Train.DetailSetupNeeded));
        }

        // With the feed loaded a ride needs no conductor of the player's own; without it, one is the only way to ride.
        if (Plugin.Instance.Feed.IsFeedLoaded)
        {
            return new Info(Kind.FeedReady, Styling.AccentMint, Styling.AccentMintSoft, FontAwesomeIcon.Rss,
                Loc.T(L.Train.TitleFeedReady), FeedDetail(Kind.FeedReady, L.Feed.HeadlineHuntAlerts, L.Train.DetailFeedReady));
        }

        if (!Conductor.IsSet)
        {
            return new Info(Kind.PickConductor, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.Flag,
                Loc.T(L.Ride.TitlePickConductor), FeedDetail(Kind.PickConductor, L.Feed.HeadlineChatOnly, L.Ride.DetailPickConductor));
        }

        return new Info(Kind.ChatOnly, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.CommentDots,
            Loc.T(L.Train.TitleChatOnly), FeedDetail(Kind.ChatOnly, L.Feed.HeadlineChatOnly, L.Train.DetailChatOnly));
    }

    // The detail line opens with which feed the trains come from; composed once per kind and language.
    private static string FeedDetail(Kind kind, LocString feedLine, LocString detail)
    {
        ref var cache = ref details[(int)kind];
        if (cache.TryGet((int)kind, out var text))
        {
            return text;
        }

        return cache.Set((int)kind, Loc.T(L.Feed.HeadlineDetail, Loc.T(feedLine), Loc.T(detail)));
    }

    public static string ShortLabel(Kind kind) => kind switch
    {
        Kind.Running       => Loc.T(L.Shell.StatusRunning),
        Kind.Paused        => Loc.T(L.Shell.StatusPaused),
        Kind.FeedReady     => Loc.T(L.Shell.StatusFeedReady),
        Kind.ChatOnly      => Loc.T(L.Shell.StatusChatOnly),
        Kind.SetupNeeded   => Loc.T(L.Shell.StatusSetupNeeded),
        Kind.PickConductor => Loc.T(L.Ride.StatusPickConductor),
        _                  => Loc.T(L.Shell.StatusIdle),
    };

    // The ride phase names what the ride is doing between marks; while a mark is engaged the hunt's own phase is the
    // finer word, fighting or travelling back after a knockout.
    public static string ActivityLabel(AutoHuntController controller)
    {
        var phase = controller.Phase;
        var ridePhase = controller.Progress.RidePhase;
        if (ridePhase is RidePhase.None or RidePhase.Engaging || phase is not (HuntPhase.Waiting or HuntPhase.Travelling))
        {
            return PhaseLabel(phase);
        }

        return RidePhaseLabel(ridePhase);
    }

    public static string ActivityDetail(AutoHuntController controller)
    {
        if (CurrentMark.TryGet(controller, out var mark))
        {
            return mark.Line;
        }

        return CurrentFlag.TryGet(controller, out var flag) ? flag.Line : controller.Status;
    }

    public static string PhaseLabel(HuntPhase phase) => phase switch
    {
        HuntPhase.Preparing  => Loc.T(L.Run.PhasePreparing),
        HuntPhase.Travelling => Loc.T(L.Run.PhaseTravelling),
        HuntPhase.Searching  => Loc.T(L.Run.PhaseSearching),
        HuntPhase.Fighting   => Loc.T(L.Run.PhaseFighting),
        HuntPhase.Upkeep     => Loc.T(L.Progress.PhaseUpkeep),
        HuntPhase.Finishing  => Loc.T(L.Run.PhaseFinishing),
        _                    => Loc.T(L.Run.PhaseStandingBy),
    };

    public static string RidePhaseLabel(RidePhase phase) => phase switch
    {
        RidePhase.Journey        => Loc.T(L.Feed.PhaseJourney),
        RidePhase.Travelling     => Loc.T(L.Ride.PhaseTravelling),
        RidePhase.AtFlag         => Loc.T(L.Ride.PhaseAtFlag),
        RidePhase.WaitingForMark => Loc.T(L.Ride.PhaseWaitingForMark),
        RidePhase.Engaging       => Loc.T(L.Run.PhaseFighting),
        _                        => Loc.T(L.Ride.PhaseWaiting),
    };
}
