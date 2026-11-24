using System.Collections.Generic;

public class ChronicleRecord
{
    public long Id { get; set; }
    public int Tick { get; set; }
    public string Category { get; set; }
    public EventPriority Priority { get; set; } = EventPriority.Normal;
    public string Title { get; set; }
    public string RawText { get; set; }
    public string FormattedBbcode { get; set; }
    public string PrimaryEntityId { get; set; }
    public string EntityTags { get; set; }
    public string MetadataJson { get; set; } = "{}";
}

public class ChronicleFilter
{
    public string EntityId { get; set; }
    public EventCategory? Category { get; set; }
    public EventPriority? MinPriority { get; set; }
    public EventPriority? ExactPriority { get; set; }
    public int? MinTick { get; set; }
    public int? MaxTick { get; set; }
    public string SearchText { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;

    public void ApplyViewMode(ChronicleViewMode mode)
    {
        MinPriority = null;
        ExactPriority = null;
        switch (mode)
        {
            case ChronicleViewMode.CondensedHigh:
                MinPriority = EventPriority.High;
                break;
            case ChronicleViewMode.DetailedNormalPlus:
                MinPriority = EventPriority.Normal;
                break;
            case ChronicleViewMode.DetailedLow:
                ExactPriority = EventPriority.Low;
                break;
            case ChronicleViewMode.DetailedAll:
            default:
                break;
        }
    }

    public static string GetViewModeLabel(ChronicleViewMode mode) => mode switch
    {
        ChronicleViewMode.CondensedHigh => "VIEW: HIGH",
        ChronicleViewMode.DetailedAll => "DETAILED: ALL",
        ChronicleViewMode.DetailedNormalPlus => "DETAILED: NORM+",
        ChronicleViewMode.DetailedLow => "DETAILED: LOW",
        _ => "VIEW: HIGH",
    };

    public static ChronicleViewMode NextViewMode(ChronicleViewMode mode) => mode switch
    {
        ChronicleViewMode.CondensedHigh => ChronicleViewMode.DetailedAll,
        ChronicleViewMode.DetailedAll => ChronicleViewMode.DetailedNormalPlus,
        ChronicleViewMode.DetailedNormalPlus => ChronicleViewMode.DetailedLow,
        _ => ChronicleViewMode.CondensedHigh,
    };
}

public enum ChronicleViewMode
{
    CondensedHigh,
    DetailedAll,
    DetailedNormalPlus,
    DetailedLow,
}
