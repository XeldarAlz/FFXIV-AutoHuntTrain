using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Train;

namespace AutoHuntTrain.Core.Tasks;

internal enum RidePhase : byte { None, Journey, WaitingForFlag, Travelling, AtFlag, WaitingForMark, Engaging }

// Where the ride's conductor came from: set by the player, named in the announcement, or picked from the first flag.
internal enum ConductorSource : byte { None, Manual, Announced, Picked }

// What the windows show of a ride while it runs: its phase, who it follows, the flag being followed, how many flags it
// followed, the mark at the flag with its health while it is fought, and the marks credited against the expansion's count.
internal sealed class RideProgress
{
    public const float UnknownHealth = -1f;

    public HuntPhase Phase { get; private set; } = HuntPhase.Idle;

    public RidePhase RidePhase { get; private set; }

    public ConductorIdentity Conductor { get; private set; } = ConductorIdentity.None;

    public ConductorSource ConductorSource { get; private set; }

    // The zone an announced train starts in, 0 when the announcement named none.
    public uint StartTerritoryId { get; private set; }

    public bool HasFlag { get; private set; }

    public FlagPost Flag { get; private set; }

    public int FlagsFollowed { get; private set; }

    public bool HasMark { get; private set; }

    public TrainMark Mark { get; private set; }

    public float MarkHealth { get; private set; } = UnknownHealth;

    public bool HasMarkHealth => MarkHealth >= 0f;

    public int MarksCredited { get; private set; }

    // Zero until the ride knows its expansion.
    public int ExpectedMarks { get; private set; }

    public void SetPhase(HuntPhase phase) => Phase = phase;

    // The coarse phase follows the ride phase, so pausing and the panels keep working while the ride waits or fights.
    public void SetRidePhase(RidePhase phase)
    {
        RidePhase = phase;
        Phase = phase switch
        {
            RidePhase.Journey => HuntPhase.Travelling,
            RidePhase.Travelling => HuntPhase.Travelling,
            RidePhase.Engaging => HuntPhase.Fighting,
            _ => HuntPhase.Waiting,
        };
    }

    public void SetConductor(in ConductorIdentity identity, ConductorSource source)
    {
        Conductor = identity;
        ConductorSource = source;
    }

    public void SetStartTerritory(uint territoryId) => StartTerritoryId = territoryId;

    public void SetFlag(in FlagPost flag)
    {
        Flag = flag;
        HasFlag = true;
    }

    public void ClearFlag()
    {
        HasFlag = false;
        Flag = default;
    }

    public void CountFlag() => FlagsFollowed++;

    public void SetMark(in TrainMark mark)
    {
        Mark = mark;
        HasMark = true;
    }

    public void SetMarkHealth(float fraction) => MarkHealth = fraction;

    public void ClearMark()
    {
        HasMark = false;
        Mark = default;
        MarkHealth = UnknownHealth;
    }

    public void SetCredits(int credited, int expected)
    {
        MarksCredited = credited;
        ExpectedMarks = expected;
    }

    public void Reset()
    {
        Phase = HuntPhase.Idle;
        RidePhase = RidePhase.None;
        Conductor = ConductorIdentity.None;
        ConductorSource = ConductorSource.None;
        StartTerritoryId = 0;
        FlagsFollowed = 0;
        MarksCredited = 0;
        ExpectedMarks = 0;
        ClearFlag();
        ClearMark();
    }
}
