using AutoHuntTrain.Core.External;
using AutoHuntTrain.Core.Ipc;
using AutoHuntTrain.Core.Train;
using clib.Services;

namespace AutoHuntTrain.Core.Tasks;

internal sealed partial class AutoHuntController
{
    private const int SessionSampleIntervalMs = 1_000;

    private readonly RideProgress progress = new();

    private AutoHuntSession? session;
    private Func<AutoHuntSession, AutoCommon>? rideTaskFactory;
    private AutoCommon? currentTask;
    private long nextSessionSampleAtMs;

    public bool Running => Svc.Automation.Running || Paused;

    public string Status => PauseReason switch
    {
        PauseReason.InContent => "Paused while you are in content",
        PauseReason.Manual    => "Paused",
        _                     => Svc.Automation.CurrentTask?.Status ?? "Idle",
    };

    public HuntPhase Phase => progress.Phase;

    public RideProgress Progress => progress;

    public AutoHuntSession? SessionSnapshot => session;

    private static void Diag(string message)
        => RunLog.Info(message);

    public void Start()
    {
        if (Running)
        {
            Diag("Start ignored: a ride is already running.");
            return;
        }

        if (!RequiredPluginsReady())
        {
            return;
        }

        var conductor = Conductor.Current;
        if (!conductor.IsSet)
        {
            Diag("Start aborted: no conductor is set.");
            ECommons.DalamudServices.Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Pick a conductor first, on the Train page or with /aht conductor First Last.");
            return;
        }

        var name = Conductor.Describe(conductor);
        BeginRun(new AutoHuntSession(), owning => new AutoRide(owning, progress, Plugin.Instance.Flags), $"following {name}");
        ECommons.DalamudServices.Svc.Chat.Print($"{AhtConstants.LogPrefix} Following {name}'s flags.");
    }

    public void Stop()
    {
        var ending = session;
        var wasPaused = Paused;
        var flagsFollowed = progress.FlagsFollowed;
        currentTask = null;
        PauseReason = PauseReason.None;
        Svc.Automation.Stop();
        if (ending is not null)
        {
            ReleaseHelpers();
        }

        // Pause already credited the run, and anything done since was the player's own play.
        FinalizeRun(ending, sample: !wasPaused);
        ClearRun();
        if (ending is null)
        {
            return;
        }

        Diag($"Stop requested after {flagsFollowed} flag(s); session cleared.");
        ECommons.DalamudServices.Svc.Chat.Print($"{AhtConstants.LogPrefix} Ride stopped after {flagsFollowed} flag(s).");
    }

    // Credits seals as they land, so the stat tiles keep pace with the wallet. A paused run is left alone, because
    // Resume makes the game state its new zero point.
    public void Tick()
    {
        if (session is null || session.Recorded || Paused)
        {
            return;
        }

        var now = Environment.TickCount64;
        if (now < nextSessionSampleAtMs)
        {
            return;
        }

        nextSessionSampleAtMs = now + SessionSampleIntervalMs;
        session.Sample();
    }

    private static bool RequiredPluginsReady()
    {
        if (ExternalPlugins.AllRequiredInstalled())
        {
            return true;
        }

        var missing = ExternalPlugins.MissingRequiredNames();
        Diag($"Start aborted: required plugins missing ({missing}).");
        ECommons.DalamudServices.Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Cannot start: install all required plugins first ({missing}).");
        return false;
    }

    // Every Resume and fault restart builds the ride task afresh from the same factory.
    private void BeginRun(AutoHuntSession newSession, Func<AutoHuntSession, AutoCommon> taskFactory, string plan)
    {
        PauseReason = PauseReason.None;
        ResetFaultBudget();
        session = newSession;
        rideTaskFactory = taskFactory;
        Diag($"Run starting: {plan}, job {newSession.JobAbbreviation}.");
        StartRide(newSession);
    }

    private void StartRide(AutoHuntSession owningSession)
    {
        progress.Reset();
        progress.SetPhase(HuntPhase.Preparing);
        RunTask(rideTaskFactory!(owningSession), () => OnRideEnded(owningSession));
    }

    private bool CanRestart() => rideTaskFactory is not null;

    // The movement library fires OnCompleted off the game thread: its await of the task does not return to the framework
    // scheduler, and the runtime moves the continuation to the thread pool. Recording a run reads the object table, which
    // Dalamud allows only on the game thread, so the hand-off is moved back there.
    private void RunTask(AutoCommon task, Action onCompleted)
    {
        currentTask = task;
        Svc.Automation.Start(task, OnCompleted: () => _ = ECommons.DalamudServices.Svc.Framework.RunOnFrameworkThread(() => HandOff(task, onCompleted)));
    }

    private void HandOff(AutoCommon task, Action onCompleted)
    {
        if (!ReferenceEquals(currentTask, task))
        {
            Diag($"{task.GetType().Name} finished but is no longer the current task (stopped, paused, or superseded); skipping hand-off.");
            return;
        }

        currentTask = null;
        onCompleted();
    }

    private void ClearRun()
    {
        session = null;
        rideTaskFactory = null;
        progress.Reset();
    }

    // A stopped task unwinds on a later frame, and after an unload that frame may never come, so the combat preset and
    // the pathfinder are released here as well.
    private static void ReleaseHelpers()
    {
        BossModIPC.Instance.ClearActive();
        BossModIPC.Instance.ReleaseMovement();
        NavmeshIPC.Instance.Stop();
    }
}

internal enum HuntPhase { Idle, Preparing, Waiting, Travelling, Searching, Fighting, Upkeep, Finishing, Paused }
