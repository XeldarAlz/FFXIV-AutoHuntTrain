using AutoHuntTrain.Core.Marks;
using AutoHuntTrain.Core.Train;

namespace AutoHuntTrain.Core.Tasks;

internal enum RidePhase : byte { None, WaitingForFlag, Travelling, AtFlag }

// What the windows show of a ride while it runs: its phase, the flag being followed, how many flags it followed, and
// the mark being hunted.
internal sealed class RideProgress
{
    public HuntPhase Phase { get; private set; } = HuntPhase.Idle;

    public RidePhase RidePhase { get; private set; }

    public bool HasFlag { get; private set; }

    public FlagPost Flag { get; private set; }

    public int FlagsFollowed { get; private set; }

    public bool HasMark { get; private set; }

    public TrainMark Mark { get; private set; }

    public void SetPhase(HuntPhase phase) => Phase = phase;

    // The coarse phase follows the ride phase, so pausing and the panels keep working while the ride waits.
    public void SetRidePhase(RidePhase phase)
    {
        RidePhase = phase;
        Phase = phase == RidePhase.Travelling ? HuntPhase.Travelling : HuntPhase.Waiting;
    }

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

    public void ClearMark()
    {
        HasMark = false;
        Mark = default;
    }

    public void Reset()
    {
        Phase = HuntPhase.Idle;
        RidePhase = RidePhase.None;
        FlagsFollowed = 0;
        ClearFlag();
        ClearMark();
    }
}
