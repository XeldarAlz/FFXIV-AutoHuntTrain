using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

// Follows the conductor's flags on the current world: each one is a leg, the newest flag always wins, and the ride
// ends on Stop or once the conductor has gone quiet for the idle limit.
internal sealed class AutoRide : AutoCommon
{
    private const string Scope = "ride";
    private const int WaitPollFrames = 10;
    // Room for a teleport, an instance change and the longest flight a zone allows, well inside the idle limit.
    private const int LegBudgetMs = 20 * TimeUnits.MillisecondsPerMinute;

    private readonly AutoHuntSession session;
    private readonly RideProgress progress;
    private readonly FlagListener listener;
    private readonly Action<FlagPost> onPosted;
    private readonly Func<bool> newerFlagArrived;

    private FlagPost pending;
    private bool hasPending;
    private bool travelling;
    private long lastFlagAtMs;
    private int legCount;

    public AutoRide(AutoHuntSession session, RideProgress progress, FlagListener listener)
    {
        this.session = session;
        this.progress = progress;
        this.listener = listener;
        onPosted = OnPosted;
        newerFlagArrived = () => hasPending;
    }

    protected override async Task Execute()
    {
        listener.Posted += onPosted;
        lastFlagAtMs = Environment.TickCount64;
        await HoldCombatMovementAndSettle(Scope);
        try
        {
            PrimeFromRecentFlags();
            BeginWaiting();
            await FollowConductor();
        }
        finally
        {
            listener.Posted -= onPosted;
            ReleaseCombatMovement(Scope);
        }
    }

    private async Task FollowConductor()
    {
        while (!CancelToken.IsCancellationRequested)
        {
            if (!TryTakePending(out var flag))
            {
                if (IdleLimitReached())
                {
                    EndOnIdle();
                    return;
                }

                await NextFrame(WaitPollFrames);
                continue;
            }

            await FollowFlag(flag);
        }
    }

    // A flag the conductor posted shortly before the ride started is still the train's current stop.
    private void PrimeFromRecentFlags()
    {
        var conductor = Conductor.Current;
        var name = Conductor.Describe(conductor);
        if (!listener.TryLatestBy(conductor, out var latest))
        {
            Diag($"Ride: no recent flag from {name}; waiting for one");
            return;
        }

        var age = DateTime.UtcNow - latest.PostedAtUtc;
        var limitSeconds = Math.Max(0, Plugin.Instance.Configuration.LateJoinLimitSeconds);
        if (age.TotalSeconds > limitSeconds)
        {
            Diag($"Ride: the last flag from {name} is {age.TotalSeconds:F0}s old, past the late-join limit of {limitSeconds}s; waiting for a new one");
            return;
        }

        Diag($"Ride: late join, the flag {name} posted {age.TotalSeconds:F0}s ago in {TerritoryNames.Of(latest.TerritoryId)} is followed first");
        pending = latest;
        hasPending = true;
    }

    private void OnPosted(FlagPost post)
    {
        var conductor = Conductor.Current;
        if (!conductor.Matches(post))
        {
            return;
        }

        pending = post;
        hasPending = true;
        var supersedes = travelling ? "; it supersedes the leg in progress" : string.Empty;
        Diag($"Ride: flag from {Conductor.Describe(conductor)} in {TerritoryNames.Of(post.TerritoryId)} at ({post.MapX:F1}, {post.MapY:F1}){InstanceText(post)} via {post.ChatType}{supersedes}");
    }

    private bool TryTakePending(out FlagPost flag)
    {
        flag = pending;
        if (!hasPending)
        {
            return false;
        }

        hasPending = false;
        lastFlagAtMs = Environment.TickCount64;
        return true;
    }

    private async Task FollowFlag(FlagPost flag)
    {
        NoteFirstFlag(flag);
        var zoneName = TerritoryNames.Of(flag.TerritoryId);
        var label = $"leg#{++legCount}";
        progress.SetFlag(flag);
        progress.SetRidePhase(RidePhase.Travelling);
        Diag($"{label}: following the flag in {zoneName} at ({flag.MapX:F1}, {flag.MapY:F1}){InstanceText(flag)}, world ({flag.WorldX:F1}, {flag.WorldZ:F1}), posted {(DateTime.UtcNow - flag.PostedAtUtc).TotalSeconds:F0}s ago ({ConditionTag()})");

        var leg = new RideLeg(flag, label);
        var completed = false;
        travelling = true;
        try
        {
            await RunWithStatusFrom(leg, async () => completed = await RunCancellable(leg, LegBudgetMs, label, newerFlagArrived));
        }
        finally
        {
            travelling = false;
        }

        if (CancelToken.IsCancellationRequested)
        {
            return;
        }

        if (!completed && hasPending)
        {
            Diag($"{label}: a newer flag arrived; the leg to {zoneName} is dropped");
            return;
        }

        if (!ReachedFlag(leg, completed, label, zoneName))
        {
            BeginWaiting();
            return;
        }

        progress.CountFlag();
        progress.SetRidePhase(RidePhase.AtFlag);
        Status = $"At the flag in {zoneName}";
        Diag($"{label}: at the flag in {zoneName}, {progress.FlagsFollowed} flag(s) followed so far ({ConditionTag()})");
        await OnArrivedAtFlag(flag);
        BeginWaiting();
    }

    private bool ReachedFlag(RideLeg leg, bool completed, string label, string zoneName)
    {
        if (leg.Fault is { } fault)
        {
            RunLog.Warning(fault, $"{label}: the leg to the flag in {zoneName} ended with an unexpected error");
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} The way to the flag in {zoneName} hit an error; waiting for the next flag. The log has the details.");
            return false;
        }

        if (!completed)
        {
            Warn($"{label}: gave up on the way to the flag in {zoneName} after {LegBudgetMs / TimeUnits.MillisecondsPerMinute} minutes");
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Could not reach the flag in {zoneName} in time; waiting for the next flag.");
            return false;
        }

        if (leg.Outcome == RideLegOutcome.Arrived)
        {
            return true;
        }

        Warn($"{label}: could not reach the flag in {zoneName} ({leg.Outcome})");
        Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Could not reach the flag in {zoneName}; waiting for the next flag. The log has the details.");
        return false;
    }

    // Phase 3 hunts the mark here.
    private static Task OnArrivedAtFlag(FlagPost flag) => Task.CompletedTask;

    private void BeginWaiting()
    {
        progress.SetRidePhase(RidePhase.WaitingForFlag);
        Status = $"Waiting for a flag from {Conductor.Describe(Conductor.Current)}";
    }

    private bool IdleLimitReached()
        => Environment.TickCount64 - lastFlagAtMs >= IdleLimitMs();

    private static long IdleLimitMs()
        => Math.Max(1, Plugin.Instance.Configuration.IdleLimitMinutes) * (long)TimeUnits.MillisecondsPerMinute;

    private void EndOnIdle()
    {
        var minutes = IdleLimitMs() / TimeUnits.MillisecondsPerMinute;
        var name = Conductor.Describe(Conductor.Current);
        session.CompletedByStopCondition = true;
        Status = "Ride ended";
        Diag($"Ride: no flag from {name} for {minutes} minutes after {progress.FlagsFollowed} flag(s); the ride ends");
        Svc.Chat.Print($"{AhtConstants.LogPrefix} No flag from {name} for {minutes} minutes; the ride ends.");
    }

    private void NoteFirstFlag(in FlagPost flag)
    {
        if (session.WorldName.Length > 0)
        {
            return;
        }

        if (Worlds.TryCurrent(out var world))
        {
            session.WorldName = world.Name;
            session.DataCenterName = world.DataCenterName;
        }

        session.Expansion = ExpansionOf(flag.TerritoryId);
        Diag($"Ride: on {session.WorldName} ({session.DataCenterName}), {session.Expansion?.ShortName() ?? "unknown expansion"}");
    }

    private static ExpansionKind? ExpansionOf(uint territoryId)
    {
        var territory = Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId);
        return territory is { } row ? ExpansionKindExtensions.FromExVersion(row.ExVersion.RowId) : null;
    }

    private static string InstanceText(in FlagPost flag)
        => flag.NamesInstance ? $" instance {flag.Instance}" : string.Empty;
}
