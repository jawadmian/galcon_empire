using Godot;

public interface IChronicleEntity
{
    string ChronicleId { get; }
    string ChronicleName { get; }
    string ChronicleType { get; }
    Vector2? WorldPosition { get; }
}

public static class ChronicleEntityExtensions
{
    public static string ToChronicleLink(this IChronicleEntity entity)
    {
        if (entity == null) return string.Empty;
        string tag = entity.ChronicleId.Contains(':')
            ? entity.ChronicleId
            : $"{entity.ChronicleType}:{entity.ChronicleId}";
        return $"[url=entity:{tag}]{entity.ChronicleName}[/url]";
    }
}
