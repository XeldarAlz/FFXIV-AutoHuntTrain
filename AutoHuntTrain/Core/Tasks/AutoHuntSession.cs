using AutoHuntTrain.Core.Marks;
using ECommons.DalamudServices;
using System.Threading;

namespace AutoHuntTrain.Core.Tasks;

// One run of the controller. The ride it carries is not built yet, so a session only keeps the clock, the job, the
// marks the kill ledger credited and what the wallet gained.
public sealed class AutoHuntSession
{
    private HuntWallet lastWallet;
    private bool walletKnown;

    private long pausedMs;
    private long pauseStartedAtMs;
    private DateTime? endedAt;

    public AutoHuntSession()
    {
        JobAbbreviation = CurrentJobAbbreviation();
        Rebaseline();
    }

    public DateTime StartedAt { get; } = DateTime.UtcNow;

    public string JobAbbreviation { get; private set; }

    // Filled in by the ride once one exists.
    public string WorldName { get; internal set; } = string.Empty;

    public string DataCenterName { get; internal set; } = string.Empty;

    public ExpansionKind? Expansion { get; internal set; }

    public int MarksCredited { get; private set; }

    public int AlliedSeals { get; private set; }

    public int CenturioSeals { get; private set; }

    public int Nuts { get; private set; }

    public bool EndedWithFault { get; private set; }

    public bool CompletedByStopCondition;

    internal bool Recorded;
    internal bool AfterActionDispatched;

    public bool DidNothing => MarksCredited == 0 && AlliedSeals == 0 && CenturioSeals == 0 && Nuts == 0;

    public int Seals => AlliedSeals + CenturioSeals;

    public TimeSpan Elapsed => (endedAt ?? DateTime.UtcNow) - StartedAt - TimeSpan.FromMilliseconds(PausedTotalMs);

    public double MarksPerHour => Elapsed.TotalHours > 0 ? MarksCredited / Elapsed.TotalHours : 0;

    private long PausedTotalMs
        => pausedMs + (pauseStartedAtMs == 0 ? 0 : Environment.TickCount64 - pauseStartedAtMs);

    internal void CreditMark() => MarksCredited++;

    public void Sample()
    {
        SampleWallet(credit: true);
        if (JobAbbreviation.Length == 0)
        {
            JobAbbreviation = CurrentJobAbbreviation();
        }
    }

    // Makes the current wallet the new zero point, so seals earned while the run was paused are not credited to it.
    public void Rebaseline() => SampleWallet(credit: false);

    // A Stop unwinds the run loop through the same catch as a genuine fault, and only a genuine fault may resume the run.
    public void RecordFault(Exception exception, CancellationToken cancelToken)
    {
        if (cancelToken.IsCancellationRequested || exception is OperationCanceledException)
        {
            return;
        }

        EndedWithFault = true;
        // clib's task runner writes the same exception to dalamud.log when the task unwinds.
        RunLog.Record(RunLogLevel.Error, exception, "The ride task ended with an unexpected error");
    }

    internal void ClearFault() => EndedWithFault = false;

    public void BeginPause()
    {
        if (pauseStartedAtMs != 0)
        {
            return;
        }

        pauseStartedAtMs = Environment.TickCount64;
    }

    public void EndPause()
    {
        if (pauseStartedAtMs == 0)
        {
            return;
        }

        pausedMs += Environment.TickCount64 - pauseStartedAtMs;
        pauseStartedAtMs = 0;
    }

    // Freezes the clock, so a finished run left on screen during its after-run action stops counting.
    internal void End()
    {
        if (endedAt is not null)
        {
            return;
        }

        EndPause();
        endedAt = DateTime.UtcNow;
    }

    private void SampleWallet(bool credit)
    {
        if (!HuntWallet.TryRead(out var wallet))
        {
            return;
        }

        if (credit && walletKnown)
        {
            AlliedSeals += CurrencyGained(lastWallet.AlliedSeals, wallet.AlliedSeals);
            CenturioSeals += CurrencyGained(lastWallet.CenturioSeals, wallet.CenturioSeals);
            Nuts += CurrencyGained(lastWallet.Nuts, wallet.Nuts);
        }

        lastWallet = wallet;
        walletKnown = true;
    }

    // Only rises count, because a spend between two samples would otherwise cancel out currency the run earned.
    private static int CurrencyGained(int before, int after) => after > before ? after - before : 0;

    private static string CurrentJobAbbreviation()
        => Svc.Objects.LocalPlayer?.ClassJob.ValueNullable?.Abbreviation.ExtractText() ?? string.Empty;
}
