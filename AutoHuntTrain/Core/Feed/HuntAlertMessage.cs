using System.Numerics;

namespace AutoHuntTrain.Core.Feed;

// Mirrors the struct HuntAlerts publishes member for member, because Dalamud converts between the two plugins' types
// by member name; the last member keeps HuntAlerts' own spelling for that reason.
public readonly record struct HuntAlertMessage(
    string Message,
    string HuntType,
    string HuntKind,
    uint HuntWorldId,
    uint CurrentWorldId,
    uint CurrentWorldRegionGroupId,
    uint HuntWorldRegionGroupId,
    DateTimeOffset PostedTime,
    long PostedEpoch,
    uint StartingAetheryteId,
    uint StartingTerritoryTypeId,
    int Instance,
    Vector2? MapLocationCoords,
    uint? creatureNameId);
