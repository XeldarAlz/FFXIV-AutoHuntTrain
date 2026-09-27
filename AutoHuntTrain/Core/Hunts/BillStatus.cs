namespace AutoHuntTrain.Core.Hunts;

public enum BillStatus : byte
{
    Locked,
    Available,
    Held,
    Stale,
    Done,
}
