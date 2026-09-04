using System.Collections.Generic;

public class ChronicleRecord
{
    public long Id { get; set; }
    public int Tick { get; set; }
    public string Category { get; set; }
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
    public int? MinTick { get; set; }
    public int? MaxTick { get; set; }
    public string SearchText { get; set; }
    public int Limit { get; set; } = 50;
    public int Offset { get; set; } = 0;
}
