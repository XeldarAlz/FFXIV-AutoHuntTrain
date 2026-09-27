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

        if (!Conductor.IsSet)
        {
            return new Info(Kind.PickConductor, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.Flag,
                Loc.T(L.Ride.TitlePickConductor), Loc.T(L.Ride.DetailPickConductor));
        }

        if (ExternalPlugins.IsInstalled(ExternalPlugin.HuntAlerts))
        {
            return new Info(Kind.FeedReady, Styling.AccentMint, Styling.AccentMintSoft, FontAwesomeIcon.Rss,
                Loc.T(L.Train.TitleFeedReady), Loc.T(L.Train.DetailFeedReady));
        }

        return new Info(Kind.ChatOnly, Styling.AccentAmber, Styling.AccentAmberSoft, FontAwesomeIcon.CommentDots,
            Loc.T(L.Train.TitleChatOnly), Loc.T(L.Train.DetailChatOnly));
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

    // The ride phase names what the ride is doing between marks; a hunt phase, once one is set, is the finer word.
    public static string ActivityLabel(AutoHuntController controller)
    {
        var phase = controller.Phase;
        if (phase is HuntPhase.Waiting or HuntPhase.Travelling && controller.Progress.RidePhase != RidePhase.None)
        {
            return RidePhaseLabel(controller.Progress.RidePhase);
        }

        return PhaseLabel(phase);
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
        RidePhase.Travelling => Loc.T(L.Ride.PhaseTravelling),
        RidePhase.AtFlag     => Loc.T(L.Ride.PhaseAtFlag),
        _                    => Loc.T(L.Ride.PhaseWaiting),
    };
}
