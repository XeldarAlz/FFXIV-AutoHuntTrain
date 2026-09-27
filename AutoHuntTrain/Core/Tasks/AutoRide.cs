using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

// Follows the conductor's flags on the current world: each one is a leg, the newest flag always wins, and at every flag
// the zone's mark is fought for credit once someone has pulled it. The ride ends on Stop, once every mark of the
// expansion is credited, or once the conductor has gone quiet for the idle limit.
internal sealed class AutoRide : AutoCommon
{
    private const string Scope = "ride";
    private const int WaitPollFrames = 10;
    private const int MarkPollFrames = 6;
    // Room for a teleport, an instance change and the longest flight a zone allows, well inside the idle limit.
    private const int LegBudgetMs = 20 * TimeUnits.MillisecondsPerMinute;
    // A conductor flags where the mark stands, and it roams a little before the pull.
    private const float FlagMarkRadiusMeters = 80f;
    private const int MarkHealthSampleMs = 250;

    private readonly AutoHuntSession session;
    private readonly RideProgress progress;
    private readonly FlagListener listener;
    private readonly Action<FlagPost> onPosted;
    private readonly Func<bool> newerFlagArrived;
    private readonly IFramework.OnUpdateDelegate sampleMarkHealth;

    private FlagPost pending;
    private bool hasPending;
    private bool travelling;
    private bool engaging;
    private long lastFlagAtMs;
    private int legCount;
    private int rankACredited;
    private MarkSighting engagedMark;
    private long nextHealthSampleAtMs;

    public AutoRide(AutoHuntSession session, RideProgress progress, FlagListener listener)
    {
        this.session = session;
        this.progress = progress;
        this.listener = listener;
        onPosted = OnPosted;
        newerFlagArrived = () => hasPending;
        sampleMarkHealth = SampleMarkHealth;
    }

    protected override async Task Execute()
    {
        listener.Posted += onPosted;
        lastFlagAtMs = Environment.TickCount64;
        await HoldCombatMovementAndSettle(Scope);
        try
        {
            PublishCredits();
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

    // The hunt's phase is the finer word while a mark is fought; Idle only marks its end, and the ride names what follows.
    private protected override void OnMarkPhaseChanged(HuntPhase phase)
    {
        if (phase != HuntPhase.Idle)
        {
            progress.SetPhase(phase);
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
            if (AllExpectedMarksCredited(out var expansion))
            {
                EndOnAllCredited(expansion);
                return;
            }
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
        var note = travelling ? "; it supersedes the leg in progress" : engaging ? "; it is taken once the fight ends" : string.Empty;
        Diag($"Ride: flag from {Conductor.Describe(conductor)} in {TerritoryNames.Of(post.TerritoryId)} at ({post.MapX:F1}, {post.MapY:F1}){InstanceText(post)} via {post.ChatType}{note}");
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
        await EngageAtFlag(flag, label, zoneName);
        progress.ClearMark();
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

    // The mark is fought to its end even when a newer flag arrives meanwhile; that flag is taken afterwards.
    private async Task EngageAtFlag(FlagPost flag, string label, string zoneName)
    {
        var arrivedAtMs = Environment.TickCount64;
        var territoryId = Svc.ClientState.TerritoryType;
        if (await WaitForPulledMark(flag, territoryId, label, zoneName) is not { } mark)
        {
            return;
        }

        var name = HuntMarkRegistry.NameOf(mark.NameId);
        progress.SetRidePhase(RidePhase.Engaging);
        Diag($"{label}: engaging {name} (rank {mark.Rank}, {PullText(mark)}) {mark.Sighting.DistanceToHitbox:F0}m away, {SecondsSince(arrivedAtMs)}s after arriving");
        var outcome = await FightSightedMark(mark, territoryId);
        var seconds = SecondsSince(arrivedAtMs);
        if (outcome != MarkOutcome.Killed)
        {
            Warn($"{label}: {name} missed ({outcome}) after {seconds}s at the flag in {zoneName}");
            return;
        }

        session.CreditMark();
        if (mark.Rank == HuntMarkRank.A)
        {
            rankACredited++;
        }

        PublishCredits();
        Diag($"{label}: {name} credited {seconds}s after arriving at the flag; {CreditTally()} credited so far");
    }

    // Stays put, mounted or not, until a mark of the zone stands near the flag and someone has pulled it, unless the
    // player would rather pull. A newer flag ends the wait: the train has moved on.
    private async Task<ZoneMarkSighting?> WaitForPulledMark(FlagPost flag, uint territoryId, string label, string zoneName)
    {
        var configuration = Plugin.Instance.Configuration;
        var waitForPull = configuration.WaitForPull;
        var budgetMs = Math.Max(0, configuration.PullWaitSeconds) * (long)TimeUnits.MillisecondsPerSecond;
        var deadline = Environment.TickCount64 + budgetMs;
        var flagPosition = new Vector3(flag.WorldX, 0f, flag.WorldZ);
        var waitingStatus = $"Waiting for the mark at the flag in {zoneName}";
        var pullStatus = waitingStatus;
        var sightedNameId = 0u;
        progress.SetRidePhase(RidePhase.WaitingForMark);
        Status = waitingStatus;
        while (!CancelToken.IsCancellationRequested)
        {
            if (hasPending)
            {
                Diag($"{label}: a newer flag arrived while waiting at the flag in {zoneName}; the train has moved on");
                return null;
            }

            if (IsMarkKnockedOut())
            {
                Diag($"{label}: knocked out while waiting at the flag in {zoneName}; recovering");
                await RecoverFromMarkKnockout();
                return null;
            }

            if (Svc.Condition[ConditionFlag.InCombat])
            {
                await FightOffAttackers(label);
            }

            if (TrySightZoneMark(territoryId, flagPosition, out var mark))
            {
                if (mark.NameId != sightedNameId)
                {
                    sightedNameId = mark.NameId;
                    pullStatus = NoteSightedMark(mark, territoryId, label);
                }

                if (mark.Pulled || !waitForPull)
                {
                    return mark;
                }

                Status = pullStatus;
            }
            else if (sightedNameId != 0)
            {
                Diag($"{label}: {HuntMarkRegistry.NameOf(sightedNameId)} left view before it was pulled");
                sightedNameId = 0;
                progress.ClearMark();
                Status = waitingStatus;
            }

            if (Environment.TickCount64 >= deadline)
            {
                Warn($"{label}: no pulled mark within {budgetMs / TimeUnits.MillisecondsPerSecond}s at the flag in {zoneName}; waiting for the next flag");
                return null;
            }

            await NextFrame(MarkPollFrames);
        }

        return null;
    }

    private static bool TrySightZoneMark(uint territoryId, Vector3 flagPosition, out ZoneMarkSighting mark)
    {
        if (Svc.Objects.LocalPlayer is not { } player)
        {
            mark = default;
            return false;
        }

        return MarkFinder.TryFindNearestZoneMark(territoryId, flagPosition, FlagMarkRadiusMeters, player.Position, out mark);
    }

    private string NoteSightedMark(in ZoneMarkSighting mark, uint territoryId, string label)
    {
        var name = HuntMarkRegistry.NameOf(mark.NameId);
        var position = mark.Sighting.Position;
        progress.SetMark(TrainMark.Flagged(mark.NameId, territoryId, position.X, position.Z));
        Diag($"{label}: {name} (rank {mark.Rank}) in view {mark.Sighting.DistanceToHitbox:F0}m away, {PullText(mark)}");
        return $"Waiting for the pull on {name}";
    }

    // The mark's health is sampled for the windows while the hunt engine fights it.
    private async Task<MarkOutcome> FightSightedMark(ZoneMarkSighting mark, uint territoryId)
    {
        engagedMark = mark.Sighting;
        nextHealthSampleAtMs = 0;
        engaging = true;
        Svc.Framework.Update += sampleMarkHealth;
        try
        {
            return await HuntSightedMark(mark.NameId, territoryId, mark.Sighting);
        }
        finally
        {
            Svc.Framework.Update -= sampleMarkHealth;
            engaging = false;
            progress.SetMarkHealth(RideProgress.UnknownHealth);
        }
    }

    private void SampleMarkHealth(IFramework _)
    {
        var now = Environment.TickCount64;
        if (now < nextHealthSampleAtMs)
        {
            return;
        }

        nextHealthSampleAtMs = now + MarkHealthSampleMs;
        progress.SetMarkHealth(MarkFinder.TryReadHealth(engagedMark, out var fraction) ? fraction : RideProgress.UnknownHealth);
    }

    private void PublishCredits() => progress.SetCredits(session.MarksCredited, ExpectedMarks.For(session.Expansion));

    private bool AllExpectedMarksCredited(out ExpansionKind expansion)
    {
        expansion = default;
        if (!Plugin.Instance.Configuration.EndWhenAllCredited || session.Expansion is not { } known)
        {
            return false;
        }

        expansion = known;
        var expected = ExpectedMarks.For(known);
        // An S rank called on the train is credited too, but only the A ranks count toward the expansion's total, or
        // the ride would end one A rank early.
        return expected > 0 && rankACredited >= expected;
    }

    private void EndOnAllCredited(ExpansionKind expansion)
    {
        var expected = ExpectedMarks.For(expansion);
        session.CompletedByStopCondition = true;
        Status = "Ride ended";
        Diag($"Ride: all {expected} A ranks of {expansion.ShortName()} credited ({session.MarksCredited} marks in all) after {progress.FlagsFollowed} flag(s); the ride ends");
        Svc.Chat.Print($"{AhtConstants.LogPrefix} All {expected} A ranks of {expansion.ShortName()} are credited; the ride ends.");
    }

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
        PublishCredits();
        Diag($"Ride: on {session.WorldName} ({session.DataCenterName}), {session.Expansion?.ShortName() ?? "unknown expansion"}, {ExpectedMarks.For(session.Expansion)} mark(s) expected");
    }

    private string CreditTally()
    {
        var expected = ExpectedMarks.For(session.Expansion);
        return expected > 0 ? $"{session.MarksCredited} of {expected}" : session.MarksCredited.ToString();
    }

    private static ExpansionKind? ExpansionOf(uint territoryId)
    {
        var territory = Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId);
        return territory is { } row ? ExpansionKindExtensions.FromExVersion(row.ExVersion.RowId) : null;
    }

    private static long SecondsSince(long startedAtMs) => (Environment.TickCount64 - startedAtMs) / TimeUnits.MillisecondsPerSecond;

    private static string PullText(in ZoneMarkSighting mark) => mark.Pulled ? "already pulled" : "not pulled yet";

    private static string InstanceText(in FlagPost flag)
        => flag.NamesInstance ? $" instance {flag.Instance}" : string.Empty;
}
