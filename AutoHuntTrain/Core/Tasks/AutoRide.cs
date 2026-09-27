using AutoHuntTrain.Core.Feed;
using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Stats;
using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using Dalamud.Game.ClientState.Conditions;
using Dalamud.Plugin.Services;
using ECommons.DalamudServices;
using Lumina.Excel.Sheets;
using System.Numerics;
using System.Threading.Tasks;

namespace AutoHuntTrain.Core.Tasks;

// Follows the conductor's flags: each one is a leg, the newest flag always wins, and at every flag the zone's mark is
// fought for credit once someone has pulled it. A ride from an announcement first travels to the train's start and,
// when the announcement named no conductor, takes the first player to post a flag there as one. The ride ends on
// Stop, once every mark of the expansion is credited, or once the conductor has gone quiet for the idle limit.
internal sealed class AutoRide : AutoCommon
{
    private const string Scope = "ride";
    private const string JourneyLabel = "ride-journey";
    private const int WaitPollFrames = 10;
    private const int MarkPollFrames = 6;
    // Room for a teleport, an instance change and the longest flight a zone allows, well inside the idle limit.
    private const int LegBudgetMs = 20 * TimeUnits.MillisecondsPerMinute;
    // A conductor flags where the mark stands, and it roams a little before the pull.
    private const float FlagMarkRadiusMeters = 80f;
    private const int MarkHealthSampleMs = 250;
    private const int ZoneTeleportWatchdogMs = 60_000;
    private const string UnknownStartZone = "the start zone";
    // A conductor flags the first mark a little before the announced start.
    private static readonly TimeSpan PickWindowBeforeStart = TimeSpan.FromMinutes(2);
    // A train is over this long after its announced start, so a data center transfer still underway by then is abandoned.
    private static readonly TimeSpan TrainDuration = TimeSpan.FromMinutes(45);

    private readonly AutoHuntSession session;
    private readonly RideProgress progress;
    private readonly FlagListener listener;
    private readonly Announcement? announcement;
    private readonly Action<FlagPost> onPosted;
    private readonly Func<bool> newerFlagArrived;
    private readonly IFramework.OnUpdateDelegate sampleMarkHealth;

    private ConductorIdentity conductor = ConductorIdentity.None;
    private ConductorIdentity manualAtStart = ConductorIdentity.None;
    private FlagPost pending;
    private bool hasPending;
    private bool travelling;
    private bool engaging;
    private long lastFlagAtMs;
    private int legCount;
    private int rankACredited;
    private MarkSighting engagedMark;
    private long nextHealthSampleAtMs;

    public AutoRide(AutoHuntSession session, RideProgress progress, FlagListener listener, Announcement? announcement = null)
    {
        this.session = session;
        this.progress = progress;
        this.listener = listener;
        this.announcement = announcement;
        onPosted = OnPosted;
        newerFlagArrived = () => hasPending;
        sampleMarkHealth = SampleMarkHealth;
    }

    internal static ExpansionKind? ExpansionOf(uint territoryId)
    {
        var territory = Svc.Data.GetExcelSheet<TerritoryType>().GetRowOrDefault(territoryId);
        return territory is { } row ? ExpansionKindExtensions.FromExVersion(row.ExVersion.RowId) : null;
    }

    protected override async Task Execute()
    {
        listener.Posted += onPosted;
        lastFlagAtMs = Environment.TickCount64;
        await HoldCombatMovementAndSettle(Scope);
        try
        {
            AdoptConductor();
            if (announcement is { } plan && !await TravelToAnnouncement(plan))
            {
                return;
            }

            PublishCredits();
            PrimeFromRecentFlags();
            BeginWaiting();
            await FollowConductor();
        }
        catch (Exception exception)
        {
            session.RecordFault(exception, CancelToken);
            throw;
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

    // A manual ride follows the conductor the player set. An announced ride follows the one it kept through a pause,
    // else the one the announcement named, else whoever posts the first flag at the start.
    private void AdoptConductor()
    {
        manualAtStart = Conductor.Current;
        if (announcement is not { } plan)
        {
            SetConductor(manualAtStart, ConductorSource.Manual);
            return;
        }

        progress.SetStartTerritory(plan.TerritoryId);
        if (session.Conductor.IsSet)
        {
            SetConductor(session.Conductor, session.ConductorSource);
            return;
        }

        if (plan.NamesConductor)
        {
            SetConductor(plan.Conductor, ConductorSource.Announced);
            Diag($"Ride: the announcement names {Conductor.Describe(plan.Conductor)} as the conductor");
            return;
        }

        SetConductor(ConductorIdentity.None, ConductorSource.None);
        Diag($"Ride: the announcement names no conductor; the first player to post a flag in {StartZoneName()} from {PickWindowBeforeStart.TotalMinutes:F0} minutes before the start becomes one");
    }

    private void SetConductor(in ConductorIdentity identity, ConductorSource source)
    {
        conductor = identity;
        session.Conductor = identity;
        session.ConductorSource = source;
        progress.SetConductor(identity, source);
    }

    // A manual ride follows whatever the player sets, at any time. On an announced ride a conductor set by hand after
    // the ride started overrides the announced or picked one; the one set before it started is an old pick.
    private ConductorIdentity RefreshConductor()
    {
        var manual = Conductor.Current;
        if (announcement is null)
        {
            if (!manual.SameAs(conductor))
            {
                SetConductor(manual, ConductorSource.Manual);
                Diag(manual.IsSet ? $"Ride: the conductor is now {Conductor.Describe(manual)}" : "Ride: the conductor was cleared; waiting until one is set");
            }

            return conductor;
        }

        if (manual.IsSet && !manual.SameAs(manualAtStart) && !manual.SameAs(conductor))
        {
            SetConductor(manual, ConductorSource.Manual);
            Diag($"Ride: {Conductor.Describe(manual)} was set by hand after the ride started and is followed instead");
        }

        return conductor;
    }

    private async Task<bool> TravelToAnnouncement(Announcement plan)
    {
        var group = ExpansionGroups.Name(plan.Group);
        if (session.ArrivedAtTrain)
        {
            Diag($"Ride: already at the start of the {group} train on {plan.World.Name} from before the pause; not travelling again");
            return true;
        }

        var resumed = session.ResumeJourney;
        session.ResumeJourney = false;
        progress.SetRidePhase(RidePhase.Journey);
        Diag($"Ride: {(resumed ? "resuming the journey" : "travelling")} to the {group} train on {plan.World.Name} ({plan.World.DataCenterName}), {LeadText(plan)}");
        var journey = JourneyPlan.ToWorld(plan.World.Name, plan.AetheryteId, plan.TerritoryId, plan.NamesAetheryte ? plan.Instance : 0);
        var outcome = await TravelToWorld(journey, resumed, plan.StartAtUtc + TrainDuration);
        if (CancelToken.IsCancellationRequested)
        {
            NoteJourneyCut(plan, group);
            return false;
        }

        if (outcome != JourneyOutcome.Arrived)
        {
            EndOnJourney(plan, group, outcome);
            return false;
        }

        if (!plan.NamesAetheryte && plan.NamesTerritory && !await TravelToStartZone(plan))
        {
            session.Outcome = RideOutcome.Abandoned;
            session.CompletedByStopCondition = !CancelToken.IsCancellationRequested;
            return false;
        }

        session.ArrivedAtTrain = true;
        lastFlagAtMs = IdleClockStart(plan);
        Diag($"Ride: at the start of the {group} train on {plan.World.Name} in {TerritoryNames.Of(Svc.ClientState.TerritoryType)}, {LeadText(plan)} ({ConditionTag()})");
        return true;
    }

    // The relay named the zone but not its aetheryte: the attuned one nearest the flagged spot, or any attuned one, is the stop.
    private async Task<bool> TravelToStartZone(Announcement plan)
    {
        var zoneName = TerritoryNames.Of(plan.TerritoryId);
        var destination = plan.MapCoordinates is { } coordinates && MapCoordinates.TryToWorld(plan.TerritoryId, coordinates, out var point) ? point : Vector3.Zero;
        var reached = false;
        await RunWithStatusPinned(
            $"Teleporting to {zoneName}",
            async () => reached = await TeleportToTerritory(plan.TerritoryId, destination, $"{JourneyLabel}-zone", ZoneTeleportWatchdogMs));
        if (!reached)
        {
            if (!CancelToken.IsCancellationRequested)
            {
                Warn($"Ride: could not reach {zoneName}, the start of the train (still in territory {Svc.ClientState.TerritoryType}); the ride ends");
                Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Could not reach {zoneName}, the start of the train; the ride ends. The log has the details.");
            }

            return false;
        }

        return !plan.NamesInstance || await SwitchToInstance(plan.Instance, JourneyLabel);
    }

    // A refused journey never moved the character, so only a journey that did go somewhere hands on to the way home.
    // A refused journey and an overdue transfer have said why in chat already.
    private void EndOnJourney(in Announcement plan, string group, JourneyOutcome outcome)
    {
        session.Outcome = RideOutcome.Abandoned;
        session.CompletedByStopCondition = outcome != JourneyOutcome.Refused;
        Warn($"Ride: the journey to the {group} train on {plan.World.Name} ended with {outcome}; the ride ends");
        if (outcome is JourneyOutcome.Refused or JourneyOutcome.Overdue)
        {
            return;
        }

        Svc.Chat.PrintError($"{AhtConstants.LogPrefix} Could not reach the {group} train on {plan.World.Name} ({outcome}); the ride ends. The log has the details.");
    }

    private void NoteJourneyCut(in Announcement plan, string group)
    {
        if (Svc.ClientState.IsLoggedIn || Plugin.Instance.Configuration.PendingRide is null)
        {
            Diag($"Ride: the journey to the {group} train on {plan.World.Name} was cancelled");
            return;
        }

        Warn($"Ride: the journey to the {group} train on {plan.World.Name} ({plan.World.DataCenterName}) was cut while logged out for the data center transfer; the ride is picked up again at login");
    }

    // A visited data center has no Grand Company mender for the character and its food may run out there, so the
    // upkeep runs once before leaving, and only when nothing the character is doing would be torn down by the relog.
    private protected override async Task<bool> PrepareDataCenterTransfer(WorldInfo target, string label)
    {
        if (TransferGuard.Blocker() is { } reason)
        {
            Warn($"{label}: the data center transfer to {target.Name} was not started: {reason}; the ride ends");
            Svc.Chat.PrintError($"{AhtConstants.LogPrefix} The trip to {target.Name} on {target.DataCenterName} was not started: {reason}. The ride ends.");
            return false;
        }

        Diag($"{label}: running the upkeep before the data center transfer to {target.Name}");
        await RunUpkeep();
        return !CancelToken.IsCancellationRequested;
    }

    private protected override void OnDataCenterTransferRequested()
    {
        session.CrossedDataCenter = true;
        if (announcement is not { } plan)
        {
            return;
        }

        Plugin.Instance.Configuration.SetPendingRide(PendingRide.From(plan, session));
        Diag($"Ride: saved the {ExpansionGroups.Name(plan.Group)} train on {plan.World.Name} for the login after the transfer");
    }

    // The idle limit counts from the announced start, so a train reached early is not given up before it begins.
    private static long IdleClockStart(in Announcement plan)
    {
        var lead = plan.LeadAt(DateTime.UtcNow);
        var leadMs = lead > TimeSpan.Zero ? (long)lead.TotalMilliseconds : 0;
        return Environment.TickCount64 + leadMs;
    }

    private static string LeadText(in Announcement plan)
    {
        var lead = plan.LeadAt(DateTime.UtcNow);
        return lead > TimeSpan.Zero ? $"it starts in {lead.TotalMinutes:F0} min" : $"it started {(-lead).TotalMinutes:F0} min ago";
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

    // A flag posted shortly before the ride started, or while the journey ran, is still the train's current stop.
    private void PrimeFromRecentFlags()
    {
        var active = RefreshConductor();
        FlagPost latest;
        if (active.IsSet)
        {
            if (!listener.TryLatestBy(active, out latest))
            {
                Diag($"Ride: no recent flag from {Conductor.Describe(active)}; waiting for one");
                return;
            }
        }
        else if (!TryLatestQualifying(out latest) || !TryPickConductor(latest))
        {
            Diag($"Ride: no flag posted in {StartZoneName()} since the train's start yet; waiting for the first one");
            return;
        }

        var age = DateTime.UtcNow - latest.PostedAtUtc;
        var limitSeconds = Math.Max(0, Plugin.Instance.Configuration.LateJoinLimitSeconds);
        if (age.TotalSeconds > limitSeconds)
        {
            Diag($"Ride: the last flag from {Conductor.Describe(conductor)} is {age.TotalSeconds:F0}s old, past the late-join limit of {limitSeconds}s; waiting for a new one");
            return;
        }

        Diag($"Ride: late join, the flag {Conductor.Describe(conductor)} posted {age.TotalSeconds:F0}s ago in {TerritoryNames.Of(latest.TerritoryId)} is followed first");
        pending = latest;
        hasPending = true;
    }

    private void OnPosted(FlagPost post)
    {
        var active = RefreshConductor();
        if (active.IsSet)
        {
            if (!active.Matches(post))
            {
                return;
            }
        }
        else if (!TryPickConductor(post))
        {
            return;
        }

        pending = post;
        hasPending = true;
        var note = travelling ? "; it supersedes the leg in progress" : engaging ? "; it is taken once the fight ends" : string.Empty;
        Diag($"Ride: flag from {Conductor.Describe(conductor)} in {TerritoryNames.Of(post.TerritoryId)} at ({post.MapX:F1}, {post.MapY:F1}){InstanceText(post)} via {post.ChatType}{note}");
    }

    // Only an announced ride picks, and only from a flag in the start zone, when known, posted from shortly before the start.
    private bool TryPickConductor(in FlagPost post)
    {
        if (announcement is not { } plan || !QualifiesAsFirstFlag(plan, post))
        {
            return false;
        }

        var picked = new ConductorIdentity(post.SenderName, post.SenderWorldId);
        SetConductor(picked, ConductorSource.Picked);
        var zoneName = TerritoryNames.Of(post.TerritoryId);
        Diag($"Ride: {Conductor.Describe(picked)} picked as the conductor from the first flag in {zoneName} at ({post.MapX:F1}, {post.MapY:F1}), posted {(DateTime.UtcNow - post.PostedAtUtc).TotalSeconds:F0}s ago via {post.ChatType}");
        Svc.Chat.Print($"{AhtConstants.LogPrefix} Following {Conductor.Describe(picked)}, the first to post a flag in {zoneName}.");
        return true;
    }

    private static bool QualifiesAsFirstFlag(in Announcement plan, in FlagPost post)
        => (!plan.NamesTerritory || post.TerritoryId == plan.TerritoryId)
        && post.PostedAtUtc >= plan.StartAtUtc - PickWindowBeforeStart;

    private bool TryLatestQualifying(out FlagPost post)
    {
        if (announcement is { } plan)
        {
            for (var index = 0; index < listener.Count; index++)
            {
                var candidate = listener.FromNewest(index);
                if (!QualifiesAsFirstFlag(plan, candidate))
                {
                    continue;
                }

                post = candidate;
                return true;
            }
        }

        post = default;
        return false;
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
        session.Outcome = RideOutcome.AllCredited;
        Status = "Ride ended";
        Diag($"Ride: all {expected} A ranks of {expansion.ShortName()} credited ({session.MarksCredited} marks in all) after {progress.FlagsFollowed} flag(s); the ride ends");
        Svc.Chat.Print($"{AhtConstants.LogPrefix} All {expected} A ranks of {expansion.ShortName()} are credited; the ride ends.");
    }

    private void BeginWaiting()
    {
        progress.SetRidePhase(RidePhase.WaitingForFlag);
        Status = conductor.IsSet
            ? $"Waiting for a flag from {Conductor.Describe(conductor)}"
            : $"Waiting for the first flag in {StartZoneName()}";
    }

    private bool IdleLimitReached()
        => Environment.TickCount64 - lastFlagAtMs >= IdleLimitMs();

    private static long IdleLimitMs()
        => Math.Max(1, Plugin.Instance.Configuration.IdleLimitMinutes) * (long)TimeUnits.MillisecondsPerMinute;

    private void EndOnIdle()
    {
        var minutes = IdleLimitMs() / TimeUnits.MillisecondsPerMinute;
        var silence = conductor.IsSet ? $"no flag from {Conductor.Describe(conductor)}" : $"no flag in {StartZoneName()}";
        session.CompletedByStopCondition = true;
        session.Outcome = RideOutcome.ConductorQuiet;
        Status = "Ride ended";
        Diag($"Ride: {silence} for {minutes} minutes after {progress.FlagsFollowed} flag(s); the ride ends");
        Svc.Chat.Print($"{AhtConstants.LogPrefix} {char.ToUpperInvariant(silence[0])}{silence[1..]} for {minutes} minutes; the ride ends.");
    }

    // The world is known from the announcement on an announced ride, and the expansion from the first flag's zone
    // unless the announcement's group already named it.
    private void NoteFirstFlag(in FlagPost flag)
    {
        if (session.WorldName.Length > 0 && session.Expansion is not null)
        {
            return;
        }

        if (session.WorldName.Length == 0 && Worlds.TryCurrent(out var world))
        {
            session.WorldName = world.Name;
            session.DataCenterName = world.DataCenterName;
        }

        session.Expansion ??= ExpansionOf(flag.TerritoryId);
        PublishCredits();
        Diag($"Ride: on {session.WorldName} ({session.DataCenterName}), {session.Expansion?.ShortName() ?? "unknown expansion"}, {ExpectedMarks.For(session.Expansion)} mark(s) expected");
    }

    private string StartZoneName()
        => announcement is { NamesTerritory: true } plan ? TerritoryNames.Of(plan.TerritoryId) : UnknownStartZone;

    private string CreditTally()
    {
        var expected = ExpectedMarks.For(session.Expansion);
        return expected > 0 ? $"{session.MarksCredited} of {expected}" : session.MarksCredited.ToString();
    }

    private static long SecondsSince(long startedAtMs) => (Environment.TickCount64 - startedAtMs) / TimeUnits.MillisecondsPerSecond;

    private static string PullText(in ZoneMarkSighting mark) => mark.Pulled ? "already pulled" : "not pulled yet";

    private static string InstanceText(in FlagPost flag)
        => flag.NamesInstance ? $" instance {flag.Instance}" : string.Empty;
}
