using AutoHuntTrain.Core.Train;
using AutoHuntTrain.Core.Travel;
using System.Numerics;

namespace AutoHuntTrain.Core.Feed;

// One train the feed announced, with everything a ride needs: where it starts, when, and who runs it when the text
// said so. Aetheryte, territory and instance 0 mean the relay could not say.
internal readonly record struct Announcement(
    int Id,
    DateTime ReceivedAtUtc,
    DateTime PostedAtUtc,
    WorldInfo World,
    ExpansionGroup Group,
    DateTime StartAtUtc,
    uint AetheryteId,
    uint TerritoryId,
    int Instance,
    Vector2? MapCoordinates,
    ConductorIdentity Conductor,
    string Message)
{
    public bool NamesConductor => Conductor.IsSet;

    public bool NamesAetheryte => AetheryteId != 0;

    public bool NamesTerritory => TerritoryId != 0;

    public bool NamesInstance => Instance > 0;

    public TimeSpan LeadAt(DateTime nowUtc) => StartAtUtc - nowUtc;

    public bool SameTrainAs(in Announcement other, TimeSpan tolerance)
        => other.World.Id == World.Id && other.Group == Group && (other.StartAtUtc - StartAtUtc).Duration() <= tolerance;
}
