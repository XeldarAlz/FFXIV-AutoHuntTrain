using AutoHuntTrain.Core.Marks;

namespace AutoHuntTrain.Core.Tasks;

// What the windows show of a ride while it runs: its phase and the mark being hunted.
internal sealed class RideProgress
{
    public HuntPhase Phase { get; private set; } = HuntPhase.Idle;

    public bool HasMark { get; private set; }

    public TrainMark Mark { get; private set; }

    public void SetPhase(HuntPhase phase) => Phase = phase;

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
        ClearMark();
    }
}
