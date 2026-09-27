using AutoHuntTrain.Core.Marks;

namespace AutoHuntTrain.Core.Stats;

[Serializable]
public sealed class RunRecord
{
    public DateTime StartedAtUtc { get; set; }
    public DateTime EndedAtUtc { get; set; }
    public double DurationSeconds { get; set; }

    public string WorldName { get; set; } = "";
    public string DataCenterName { get; set; } = "";
    public ExpansionKind? Expansion { get; set; }

    public int MarksCredited { get; set; }
    public int AlliedSeals { get; set; }
    public int CenturioSeals { get; set; }
    public int Nuts { get; set; }

    public string JobAbbreviation { get; set; } = "";

    public TimeSpan Duration => TimeSpan.FromSeconds(DurationSeconds);
    public int Seals => AlliedSeals + CenturioSeals;
    public double MarksPerHour => DurationSeconds > 0 ? MarksCredited / (DurationSeconds / 3600.0) : 0;
}
