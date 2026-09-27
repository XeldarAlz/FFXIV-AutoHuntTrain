using AutoHuntTrain.Core.Feed;

namespace AutoHuntTrain;

public sealed partial class Configuration
{
    // Whether trains of each expansion group are auto-joined; every train can be ridden with its Ride button anyway.
    public bool RideCenturio { get; set; } = false;

    public bool RideShadowbringers { get; set; } = false;

    public bool RideEndwalker { get; set; } = false;

    public bool RideDawntrail { get; set; } = false;

    // Data center names; empty means the home data center only.
    public string[] AllowedDataCenters { get; set; } = [];

    public bool AllowCrossDataCenterRides { get; set; } = false;

    public int MinimumLeadSeconds { get; set; } = 180;

    // The old master switch, read only by the version 1 migration and never written back.
    public bool AutoRide { get; set; } = false;

    public DateTime? SnoozeUntilUtc { get; set; }

    public int SnoozeMinutes { get; set; } = 30;

    // Only what the Upcoming list and the chat line show. A config saved before the view picker existed keeps the old
    // My data centers default; only a fresh config starts on Everywhere.
    public TrainListView TrainListView { get; set; } = TrainListView.MyDataCenters;

    public RelayChannel RelayChannel { get; set; } = RelayChannel.Party;

    public bool RelayWithFlag { get; set; } = true;

    public bool ShouldSerializeAutoRide() => false;

    public bool IsAutoJoinActive() => RideCenturio || RideShadowbringers || RideEndwalker || RideDawntrail;

    public void DisableAutoJoin()
    {
        RideCenturio = false;
        RideShadowbringers = false;
        RideEndwalker = false;
        RideDawntrail = false;
    }

    public bool IsGroupEnabled(ExpansionGroup group) => group switch
    {
        ExpansionGroup.Centurio => RideCenturio,
        ExpansionGroup.Shadowbringers => RideShadowbringers,
        ExpansionGroup.Endwalker => RideEndwalker,
        _ => RideDawntrail,
    };

    public void SetGroupEnabled(ExpansionGroup group, bool enabled)
    {
        switch (group)
        {
            case ExpansionGroup.Centurio:
                RideCenturio = enabled;
                break;
            case ExpansionGroup.Shadowbringers:
                RideShadowbringers = enabled;
                break;
            case ExpansionGroup.Endwalker:
                RideEndwalker = enabled;
                break;
            default:
                RideDawntrail = enabled;
                break;
        }
    }

    public bool ListsDataCenter(string name)
    {
        var allowed = AllowedDataCenters;
        for (var index = 0; index < allowed.Length; index++)
        {
            if (string.Equals(allowed[index], name, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }

    public void SetDataCenterAllowed(string name, bool allowed)
    {
        if (allowed == ListsDataCenter(name))
        {
            return;
        }

        var current = AllowedDataCenters;
        if (allowed)
        {
            var grown = new string[current.Length + 1];
            Array.Copy(current, grown, current.Length);
            grown[current.Length] = name;
            AllowedDataCenters = grown;
            return;
        }

        var shrunk = new string[current.Length - 1];
        var written = 0;
        for (var index = 0; index < current.Length; index++)
        {
            if (string.Equals(current[index], name, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            shrunk[written++] = current[index];
        }

        AllowedDataCenters = shrunk;
    }

    public bool IsSnoozed(DateTime nowUtc) => SnoozeUntilUtc is { } until && until > nowUtc;
}
