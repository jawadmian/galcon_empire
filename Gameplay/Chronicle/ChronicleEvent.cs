using System;
using System.Collections.Generic;
using System.Text.Json;
using System.Text.RegularExpressions;
using Godot;

public static class ChronicleEvent
{
    public static ChronicleEventBuilder Create(EventCategory category, string title)
    {
        return new ChronicleEventBuilder(category, title);
    }
}

public class ChronicleEventBuilder
{
    private readonly EventCategory _category;
    private readonly string _title;
    private string _template = string.Empty;
    private IChronicleEntity _primaryEntity;
    private readonly Dictionary<string, IChronicleEntity> _involvedEntities = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, string> _customArgs = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, object> _metadata = new(StringComparer.OrdinalIgnoreCase);

    public ChronicleEventBuilder(EventCategory category, string title)
    {
        _category = category;
        _title = title;
    }

    public ChronicleEventBuilder Involving(IChronicleEntity entity, bool isPrimary = false)
    {
        if (entity == null) return this;

        if (isPrimary || _primaryEntity == null)
        {
            _primaryEntity = entity;
        }

        // Store by ChronicleType (e.g. "star", "empire", "leader")
        if (!_involvedEntities.ContainsKey(entity.ChronicleType))
        {
            _involvedEntities[entity.ChronicleType] = entity;
        }
        // Also store by full ChronicleId (e.g. "star:sol")
        _involvedEntities[entity.ChronicleId] = entity;

        return this;
    }

    public ChronicleEventBuilder WithTemplate(string template)
    {
        _template = template ?? string.Empty;
        return this;
    }

    public ChronicleEventBuilder WithArg(string key, object value)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _customArgs[key] = value?.ToString() ?? string.Empty;
        }
        return this;
    }

    public ChronicleEventBuilder WithMetadata(string key, object value)
    {
        if (!string.IsNullOrEmpty(key))
        {
            _metadata[key] = value;
        }
        return this;
    }

    public ChronicleRecord Record()
    {
        int currentTick = 0;
        if (TickManager.Instance != null)
        {
            currentTick = TickManager.Instance.tickCount;
        }

        // Parse template for both BBCode (hyperlinked) and raw text
        string formattedBbcode = FormatTokens(_template, useHyperlinks: true);
        string rawText = FormatTokens(_template, useHyperlinks: false);

        // Build comma-enclosed entity tags string: ",star:sol,empire:1,"
        var tagSet = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var entity in _involvedEntities.Values)
        {
            tagSet.Add(entity.ChronicleId);
        }

        string tagsString = tagSet.Count > 0 ? $",{string.Join(",", tagSet)}," : string.Empty;

        var record = new ChronicleRecord
        {
            Tick = currentTick,
            Category = _category.ToString(),
            Title = _title,
            RawText = rawText,
            FormattedBbcode = formattedBbcode,
            PrimaryEntityId = _primaryEntity?.ChronicleId ?? string.Empty,
            EntityTags = tagsString,
            MetadataJson = _metadata.Count > 0 ? JsonSerializer.Serialize(_metadata) : "{}"
        };

        // Dispatch to ChronicleManager if available
        if (ChronicleManager.Instance != null)
        {
            ChronicleManager.Instance.RecordEvent(record);
        }

        return record;
    }

    private string FormatTokens(string template, bool useHyperlinks)
    {
        if (string.IsNullOrEmpty(template)) return string.Empty;

        return Regex.Replace(template, @"\{(\w+)\}", match =>
        {
            string token = match.Groups[1].Value;

            if (_involvedEntities.TryGetValue(token, out var entity))
            {
                return useHyperlinks ? entity.ToChronicleLink() : entity.ChronicleName;
            }

            if (_customArgs.TryGetValue(token, out var customVal))
            {
                return customVal;
            }

            return match.Value; // Leave unrecognized token intact
        });
    }
}
