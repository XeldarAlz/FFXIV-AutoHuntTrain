using AutoHuntTrain.Core.Feed;

namespace AutoHuntTrain;

public sealed partial class Configuration
{
    // Written before a data center transfer and cleared when the ride ends for any reason but the relog, so a login
    // can rebuild the ride when its task did not live through the transfer. Saved at once, since a crash in the queue
    // would lose a debounced write.
    public PendingRide? PendingRide { get; set; }

    public void SetPendingRide(PendingRide ride)
    {
        PendingRide = ride;
        Save();
    }

    public void ClearPendingRide()
    {
        if (PendingRide is null)
        {
            return;
        }

        PendingRide = null;
        SaveDebounced();
    }
}
