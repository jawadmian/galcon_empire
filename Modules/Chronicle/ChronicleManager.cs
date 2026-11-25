using System;
using System.Collections.Generic;
using Godot;
using Microsoft.Data.Sqlite;

public partial class ChronicleManager : Node
{
    private static ChronicleManager _instance;
    public static ChronicleManager Instance
    {
        get => _instance;
        private set => _instance = value;
    }

    public static void SetInstanceForTesting(ChronicleManager manager)
    {
        _instance = manager;
    }

    /// <summary>
    /// Optional delegate supplying the current simulation tick/turn/timestamp when recording events.
    /// Wire this in your game's clock or bootstrap (e.g. <c>ChronicleManager.TickProvider = () => Clock.Tick;</c>).
    /// Defaults to returning 0 when unset.
    /// </summary>
    public static Func<int> TickProvider { get; set; }

    public static int GetCurrentTick() => TickProvider?.Invoke() ?? 0;

    [Signal]
    public delegate void EventRecordedEventHandler(long eventId, string title, string category, int tick);

    [Signal]
    public delegate void EntityLinkClickedEventHandler(string entityType, string chronicleId);

    [Signal]
    public delegate void TombstoneLinkClickedEventHandler(string entityType, string chronicleId);

    private readonly Dictionary<string, IChronicleEntity> _entityRegistry =
        new Dictionary<string, IChronicleEntity>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Func<string, IChronicleEntity>> _entityResolvers =
        new Dictionary<string, Func<string, IChronicleEntity>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Action<IChronicleEntity, Node>> _linkHandlers =
        new Dictionary<string, Action<IChronicleEntity, Node>>(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, Action<ChronicleTombstone, Node>> _tombstoneHandlers =
        new Dictionary<string, Action<ChronicleTombstone, Node>>(StringComparer.OrdinalIgnoreCase);
    private Action<ChronicleTombstone, Node> _defaultTombstoneHandler;

    [Export]
    public string DatabasePath { get; set; } = "res://saves/chronicle.db";

    [Export]
    public string TestDatabasePath { get; set; } = "res://saves/chronicle_test.db";

    [Export]
    public bool ResetOnStart { get; set; } = false;

    private SqliteConnection _dbConnection;
    private readonly object _dbLock = new object();

    public static bool IsRunningTests()
    {
        foreach (string arg in OS.GetCmdlineArgs())
        {
            if (arg.Contains("gdUnit", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        foreach (string arg in OS.GetCmdlineUserArgs())
        {
            if (arg.Contains("gdUnit", StringComparison.OrdinalIgnoreCase))
                return true;
        }
        return false;
    }

    public override void _EnterTree()
    {
        if (_instance != null && _instance != this)
        {
            GD.PushWarning($"Duplicate ChronicleManager detected. Removing duplicate: {GetPath()}");
            QueueFree();
            return;
        }

        Instance = this;
    }

    public override void _Ready()
    {
        if (IsRunningTests())
        {
            DatabasePath = TestDatabasePath;
            InitializeDatabase(resetDatabase: true);
        }
        else
        {
            InitializeDatabase(resetDatabase: ResetOnStart);
        }
    }

    public override void _ExitTree()
    {
        CloseDatabase();
        if (_instance == this)
        {
            _instance = null;
        }
    }

    /// <summary>
    /// Initializes the SQLite database and executes table schema setup.
    /// Can be passed a custom connection string (e.g. "Data Source=:memory:") for testing,
    /// and optionally resets the database for a new game run.
    /// </summary>
    public void InitializeDatabase(string customConnectionString = null, bool resetDatabase = false)
    {
        lock (_dbLock)
        {
            CloseDatabase();

            string connectionString;
            string globalPath = null;
            if (!string.IsNullOrEmpty(customConnectionString))
            {
                connectionString = customConnectionString;
            }
            else
            {
                globalPath = ProjectSettings.GlobalizePath(DatabasePath);
                string dirPath = System.IO.Path.GetDirectoryName(globalPath);
                if (!string.IsNullOrEmpty(dirPath) && !System.IO.Directory.Exists(dirPath))
                {
                    System.IO.Directory.CreateDirectory(dirPath);
                }

                if (resetDatabase && System.IO.File.Exists(globalPath))
                {
                    DeleteDatabaseFiles(globalPath);
                }

                connectionString = $"Data Source={globalPath}";
            }

            try
            {
                OpenAndSetupDatabase(connectionString, resetDatabase);
            }
            catch (SqliteException ex) when (globalPath != null && (ex.SqliteErrorCode == 11 || ex.Message.Contains("malformed")))
            {
                GD.PushWarning($"Chronicle database at {globalPath} was corrupt ({ex.Message}). Recreating clean database...");
                CloseDatabase();
                DeleteDatabaseFiles(globalPath);
                OpenAndSetupDatabase(connectionString, resetDatabase);
            }
        }
    }

    private void DeleteDatabaseFiles(string globalPath)
    {
        try
        {
            SqliteConnection.ClearAllPools();
            if (System.IO.File.Exists(globalPath)) System.IO.File.Delete(globalPath);
            string walPath = globalPath + "-wal";
            string shmPath = globalPath + "-shm";
            if (System.IO.File.Exists(walPath)) System.IO.File.Delete(walPath);
            if (System.IO.File.Exists(shmPath)) System.IO.File.Delete(shmPath);
        }
        catch (Exception ex)
        {
            GD.PushWarning($"Could not delete chronicle database file: {ex.Message}");
        }
    }

    private void OpenAndSetupDatabase(string connectionString, bool resetDatabase)
    {
        _dbConnection = new SqliteConnection(connectionString);
        _dbConnection.Open();

        using (var pragmaCmd = _dbConnection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA journal_mode = WAL; PRAGMA synchronous = NORMAL;";
            pragmaCmd.ExecuteNonQuery();
        }

        ExecuteSchemaSetup();

        if (resetDatabase)
        {
            ClearHistory();
        }
    }

    private void ExecuteSchemaSetup()
    {
        const string schemaSql = @"
            CREATE TABLE IF NOT EXISTS chronicle_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                tick INTEGER NOT NULL,
                category TEXT NOT NULL,
                priority INTEGER NOT NULL DEFAULT 1,
                title TEXT NOT NULL,
                raw_text TEXT NOT NULL,
                formatted_bbcode TEXT NOT NULL,
                primary_entity_id TEXT,
                entity_tags TEXT NOT NULL,
                metadata_json TEXT DEFAULT '{}'
            );

            CREATE INDEX IF NOT EXISTS idx_chronicle_tick ON chronicle_events(tick DESC);
            CREATE INDEX IF NOT EXISTS idx_chronicle_category ON chronicle_events(category);
            CREATE INDEX IF NOT EXISTS idx_chronicle_primary_entity ON chronicle_events(primary_entity_id);
            CREATE INDEX IF NOT EXISTS idx_chronicle_entity_tags ON chronicle_events(entity_tags);

            CREATE TABLE IF NOT EXISTS chronicle_entities (
                chronicle_id TEXT PRIMARY KEY,
                entity_type TEXT NOT NULL,
                display_name TEXT NOT NULL,
                status TEXT NOT NULL DEFAULT 'Active',
                last_known_tick INTEGER NOT NULL DEFAULT 0,
                summary_json TEXT DEFAULT '{}'
            );
        ";

        using var cmd = _dbConnection.CreateCommand();
        cmd.CommandText = schemaSql;
        cmd.ExecuteNonQuery();

        // Migrate older databases that predate the priority column
        bool hasPriorityCol = false;
        using (var pragmaCmd = _dbConnection.CreateCommand())
        {
            pragmaCmd.CommandText = "PRAGMA table_info(chronicle_events);";
            using var reader = pragmaCmd.ExecuteReader();
            while (reader.Read())
            {
                if (reader.GetString(1).Equals("priority", StringComparison.OrdinalIgnoreCase))
                {
                    hasPriorityCol = true;
                    break;
                }
            }
        }

        if (!hasPriorityCol)
        {
            using var alterCmd = _dbConnection.CreateCommand();
            alterCmd.CommandText = "ALTER TABLE chronicle_events ADD COLUMN priority INTEGER NOT NULL DEFAULT 1;";
            alterCmd.ExecuteNonQuery();
        }

        using (var idxCmd = _dbConnection.CreateCommand())
        {
            idxCmd.CommandText = "CREATE INDEX IF NOT EXISTS idx_chronicle_priority ON chronicle_events(priority);";
            idxCmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Inserts a new event record into the SQLite chronicle.
    /// </summary>
    public void RecordEvent(ChronicleRecord record)
    {
        if (record == null) return;

        lock (_dbLock)
        {
            if (_dbConnection == null || _dbConnection.State != System.Data.ConnectionState.Open)
            {
                return;
            }

            try
            {
                const string insertSql = @"
                    INSERT INTO chronicle_events (
                        tick, category, priority, title, raw_text, formatted_bbcode, primary_entity_id, entity_tags, metadata_json
                    ) VALUES (
                        @tick, @category, @priority, @title, @raw_text, @formatted_bbcode, @primary_entity_id, @entity_tags, @metadata_json
                    );
                    SELECT last_insert_rowid();
                ";

                using var cmd = _dbConnection.CreateCommand();
                cmd.CommandText = insertSql;
                cmd.Parameters.AddWithValue("@tick", record.Tick);
                cmd.Parameters.AddWithValue("@category", record.Category ?? string.Empty);
                cmd.Parameters.AddWithValue("@priority", (int)record.Priority);
                cmd.Parameters.AddWithValue("@title", record.Title ?? string.Empty);
                cmd.Parameters.AddWithValue("@raw_text", record.RawText ?? string.Empty);
                cmd.Parameters.AddWithValue("@formatted_bbcode", record.FormattedBbcode ?? string.Empty);
                cmd.Parameters.AddWithValue("@primary_entity_id", record.PrimaryEntityId ?? string.Empty);
                cmd.Parameters.AddWithValue("@entity_tags", record.EntityTags ?? string.Empty);
                cmd.Parameters.AddWithValue("@metadata_json", record.MetadataJson ?? "{}");

                var scalarResult = cmd.ExecuteScalar();
                if (scalarResult != null)
                {
                    record.Id = Convert.ToInt64(scalarResult);
                }

                CallDeferred(
                    GodotObject.MethodName.EmitSignal,
                    SignalName.EventRecorded,
                    record.Id,
                    record.Title,
                    record.Category,
                    record.Tick
                );
            }
            catch (Exception ex)
            {
                GD.PrintErr($"ChronicleManager: Failed to record event '{record.Title}': {ex.Message}");
            }
        }
    }

    /// <summary>
    /// Queries the chronicle with filtering, sorting, and pagination.
    /// </summary>
    public List<ChronicleRecord> QueryEvents(ChronicleFilter filter = null)
    {
        filter ??= new ChronicleFilter();
        var results = new List<ChronicleRecord>();

        lock (_dbLock)
        {
            if (_dbConnection == null || _dbConnection.State != System.Data.ConnectionState.Open)
            {
                return results;
            }

            var queryBuilder = new System.Text.StringBuilder(@"
                SELECT id, tick, category, priority, title, raw_text, formatted_bbcode, primary_entity_id, entity_tags, metadata_json
                FROM chronicle_events
                WHERE 1=1
            ");

            using var cmd = _dbConnection.CreateCommand();

            AppendFilterConditions(queryBuilder, cmd, filter);

            queryBuilder.Append(" ORDER BY tick DESC, id DESC LIMIT @limit OFFSET @offset;");
            cmd.Parameters.AddWithValue("@limit", Math.Max(1, filter.Limit));
            cmd.Parameters.AddWithValue("@offset", Math.Max(0, filter.Offset));

            cmd.CommandText = queryBuilder.ToString();

            using var reader = cmd.ExecuteReader();
            while (reader.Read())
            {
                results.Add(new ChronicleRecord
                {
                    Id = reader.GetInt64(0),
                    Tick = reader.GetInt32(1),
                    Category = reader.GetString(2),
                    Priority = (EventPriority)reader.GetInt32(3),
                    Title = reader.GetString(4),
                    RawText = reader.GetString(5),
                    FormattedBbcode = reader.GetString(6),
                    PrimaryEntityId = reader.IsDBNull(7) ? string.Empty : reader.GetString(7),
                    EntityTags = reader.GetString(8),
                    MetadataJson = reader.IsDBNull(9) ? "{}" : reader.GetString(9)
                });
            }
        }

        return results;
    }

    private static void AppendFilterConditions(
        System.Text.StringBuilder queryBuilder,
        SqliteCommand cmd,
        ChronicleFilter filter
    )
    {
        if (!string.IsNullOrEmpty(filter.EntityId))
        {
            queryBuilder.Append(" AND entity_tags LIKE @entity_tag");
            cmd.Parameters.AddWithValue("@entity_tag", $"%,{filter.EntityId},%");
        }

        if (!string.IsNullOrEmpty(filter.CategoryName))
        {
            queryBuilder.Append(" AND category = @category");
            cmd.Parameters.AddWithValue("@category", filter.CategoryName);
        }
        else if (filter.Category.HasValue)
        {
            queryBuilder.Append(" AND category = @category");
            cmd.Parameters.AddWithValue("@category", filter.Category.Value.ToString());
        }

        if (filter.MinPriority.HasValue)
        {
            queryBuilder.Append(" AND priority >= @min_priority");
            cmd.Parameters.AddWithValue("@min_priority", (int)filter.MinPriority.Value);
        }

        if (filter.ExactPriority.HasValue)
        {
            queryBuilder.Append(" AND priority = @exact_priority");
            cmd.Parameters.AddWithValue("@exact_priority", (int)filter.ExactPriority.Value);
        }

        if (filter.MinTick.HasValue)
        {
            queryBuilder.Append(" AND tick >= @min_tick");
            cmd.Parameters.AddWithValue("@min_tick", filter.MinTick.Value);
        }

        if (filter.MaxTick.HasValue)
        {
            queryBuilder.Append(" AND tick <= @max_tick");
            cmd.Parameters.AddWithValue("@max_tick", filter.MaxTick.Value);
        }

        if (!string.IsNullOrEmpty(filter.SearchText))
        {
            queryBuilder.Append(" AND (raw_text LIKE @search OR title LIKE @search)");
            cmd.Parameters.AddWithValue("@search", $"%{filter.SearchText}%");
        }
    }

    /// <summary>
    /// Returns the total matching record count for a given filter.
    /// </summary>
    public int GetEventCount(ChronicleFilter filter = null)
    {
        filter ??= new ChronicleFilter();

        lock (_dbLock)
        {
            if (_dbConnection == null || _dbConnection.State != System.Data.ConnectionState.Open)
            {
                return 0;
            }

            var queryBuilder = new System.Text.StringBuilder(@"
                SELECT COUNT(*) FROM chronicle_events WHERE 1=1
            ");

            using var cmd = _dbConnection.CreateCommand();
            AppendFilterConditions(queryBuilder, cmd, filter);

            cmd.CommandText = queryBuilder.ToString();
            var countResult = cmd.ExecuteScalar();
            return countResult != null ? Convert.ToInt32(countResult) : 0;
        }
    }

    /// <summary>
    /// Clears all stored events (useful for resetting or starting new games).
    /// </summary>
    public void ClearHistory()
    {
        lock (_dbLock)
        {
            if (_dbConnection == null || _dbConnection.State != System.Data.ConnectionState.Open)
            {
                return;
            }

            using var cmd = _dbConnection.CreateCommand();
            cmd.CommandText = @"
                DELETE FROM chronicle_events;
                DELETE FROM chronicle_entities;
                DELETE FROM sqlite_sequence WHERE name='chronicle_events';
            ";
            cmd.ExecuteNonQuery();
        }
    }

    /// <summary>
    /// Resets the world state and clears all chronicle events for a new game run.
    /// </summary>
    public void ResetWorldState()
    {
        ClearHistory();
        lock (_dbLock)
        {
            _entityRegistry.Clear();
        }
    }

    /// <summary>
    /// Registers a live or historical entity so BBCode hyperlinks (<c>entity:type:id</c>)
    /// can resolve back to the entity instance when clicked in UI panels, and persists
    /// an archive snapshot for tombstone fallback if the entity later leaves the game.
    /// </summary>
    public void RegisterEntity(IChronicleEntity entity, int? tick = null)
    {
        if (entity == null || string.IsNullOrEmpty(entity.ChronicleId))
            return;

        lock (_dbLock)
        {
            _entityRegistry[entity.ChronicleId] = entity;
            UpsertEntitySnapshotLocked(
                entity.ChronicleId,
                entity.ChronicleType,
                entity.ChronicleName,
                "Active",
                tick ?? GetCurrentTick(),
                null
            );
        }
    }

    /// <summary>
    /// Marks an entity as tombstoned (no longer active in the game), removing it from the
    /// live entity cache and persisting its final status and optional summary metadata.
    /// </summary>
    public void MarkEntityTombstoned(
        IChronicleEntity entity,
        string status = "Archived",
        Dictionary<string, object> summaryMetadata = null,
        int? tick = null
    )
    {
        if (entity == null || string.IsNullOrEmpty(entity.ChronicleId))
            return;

        string summaryJson = summaryMetadata != null && summaryMetadata.Count > 0
            ? System.Text.Json.JsonSerializer.Serialize(summaryMetadata)
            : null;

        lock (_dbLock)
        {
            _entityRegistry.Remove(entity.ChronicleId);
            UpsertEntitySnapshotLocked(
                entity.ChronicleId,
                entity.ChronicleType,
                entity.ChronicleName,
                string.IsNullOrEmpty(status) ? "Archived" : status,
                tick ?? GetCurrentTick(),
                summaryJson
            );
        }
    }

    private void UpsertEntitySnapshotLocked(
        string chronicleId,
        string entityType,
        string displayName,
        string status,
        int tick,
        string summaryJson
    )
    {
        if (_dbConnection == null || _dbConnection.State != System.Data.ConnectionState.Open)
            return;

        try
        {
            const string sql = @"
                INSERT INTO chronicle_entities (
                    chronicle_id, entity_type, display_name, status, last_known_tick, summary_json
                ) VALUES (
                    @id, @type, @name, @status, @tick, COALESCE(@summary, '{}')
                )
                ON CONFLICT(chronicle_id) DO UPDATE SET
                    entity_type = excluded.entity_type,
                    display_name = excluded.display_name,
                    status = CASE WHEN excluded.status = 'Active' AND chronicle_entities.status != 'Active' THEN chronicle_entities.status ELSE excluded.status END,
                    last_known_tick = MAX(chronicle_entities.last_known_tick, excluded.last_known_tick),
                    summary_json = CASE WHEN @summary IS NOT NULL THEN @summary ELSE chronicle_entities.summary_json END;
            ";

            using var cmd = _dbConnection.CreateCommand();
            cmd.CommandText = sql;
            cmd.Parameters.AddWithValue("@id", chronicleId);
            cmd.Parameters.AddWithValue("@type", entityType ?? string.Empty);
            cmd.Parameters.AddWithValue("@name", displayName ?? string.Empty);
            cmd.Parameters.AddWithValue("@status", status ?? "Active");
            cmd.Parameters.AddWithValue("@tick", tick);
            cmd.Parameters.AddWithValue("@summary", (object)summaryJson ?? DBNull.Value);
            cmd.ExecuteNonQuery();
        }
        catch (Exception ex)
        {
            GD.PrintErr($"ChronicleManager: Failed to upsert entity snapshot '{chronicleId}': {ex.Message}");
        }
    }

    /// <summary>
    /// Retrieves an archived tombstone snapshot for an entity that is no longer active in the game.
    /// Falls back to reconstructing basic identity from <c>chronicle_events</c> if needed.
    /// </summary>
    public ChronicleTombstone GetTombstone(string chronicleId)
    {
        if (string.IsNullOrEmpty(chronicleId))
            return null;

        string normalized = chronicleId.StartsWith("entity:", StringComparison.OrdinalIgnoreCase)
            ? chronicleId.Substring("entity:".Length)
            : chronicleId;

        lock (_dbLock)
        {
            if (_dbConnection == null || _dbConnection.State != System.Data.ConnectionState.Open)
                return null;

            using (var cmd = _dbConnection.CreateCommand())
            {
                cmd.CommandText = @"
                    SELECT chronicle_id, entity_type, display_name, status, last_known_tick, summary_json
                    FROM chronicle_entities
                    WHERE chronicle_id = @id
                    LIMIT 1;
                ";
                cmd.Parameters.AddWithValue("@id", normalized);
                using var reader = cmd.ExecuteReader();
                if (reader.Read())
                {
                    string status = reader.GetString(3);
                    return new ChronicleTombstone
                    {
                        ChronicleId = reader.GetString(0),
                        EntityType = reader.GetString(1),
                        DisplayName = reader.GetString(2),
                        Status = string.Equals(status, "Active", StringComparison.OrdinalIgnoreCase)
                            ? "No Longer Active"
                            : status,
                        LastKnownTick = reader.GetInt32(4),
                        SummaryJson = reader.IsDBNull(5) ? "{}" : reader.GetString(5),
                    };
                }
            }

            // Fallback: reconstruct from historical events if entity was never explicitly snapshotted
            var fallbackFilter = new ChronicleFilter { EntityId = normalized, Limit = 1 };
            var events = QueryEvents(fallbackFilter);
            if (events.Count > 0)
            {
                int colonIdx = normalized.IndexOf(':');
                string entityType = colonIdx > 0 ? normalized.Substring(0, colonIdx) : "entity";
                string displayName = ExtractLinkTextForId(events[0].FormattedBbcode, normalized) ?? normalized;
                return new ChronicleTombstone
                {
                    ChronicleId = normalized,
                    EntityType = entityType,
                    DisplayName = displayName,
                    Status = "No Longer Active",
                    LastKnownTick = events[0].Tick,
                    SummaryJson = "{}",
                };
            }
        }

        return null;
    }

    private static string ExtractLinkTextForId(string bbcode, string chronicleId)
    {
        if (string.IsNullOrEmpty(bbcode) || string.IsNullOrEmpty(chronicleId))
            return null;

        string prefix = $"[url=entity:{chronicleId}]";
        int start = bbcode.IndexOf(prefix, StringComparison.OrdinalIgnoreCase);
        if (start < 0)
            return null;

        int textStart = start + prefix.Length;
        int end = bbcode.IndexOf("[/url]", textStart, StringComparison.OrdinalIgnoreCase);
        return end > textStart ? bbcode.Substring(textStart, end - textStart) : null;
    }

    /// <summary>
    /// Removes an entity from the hyperlink resolution cache.
    /// </summary>
    public void UnregisterEntity(string chronicleId)
    {
        if (string.IsNullOrEmpty(chronicleId))
            return;

        lock (_dbLock)
        {
            _entityRegistry.Remove(chronicleId);
        }
    }

    /// <summary>
    /// Registers a fallback resolver for a given <see cref="IChronicleEntity.ChronicleType"/>
    /// (e.g. querying a manager when an entity wasn't cached directly).
    /// </summary>
    public void RegisterEntityResolver(string entityType, Func<string, IChronicleEntity> resolver)
    {
        if (string.IsNullOrEmpty(entityType) || resolver == null)
            return;

        _entityResolvers[entityType] = resolver;
    }

    /// <summary>
    /// Registers a typed click handler for a given entity type (e.g. opening its inspector panel).
    /// </summary>
    public void RegisterLinkHandler<T>(string entityType, Action<T, Node> handler)
        where T : class, IChronicleEntity
    {
        if (string.IsNullOrEmpty(entityType) || handler == null)
            return;

        _linkHandlers[entityType] = (entity, contextNode) =>
        {
            if (entity is T typed)
            {
                handler(typed, contextNode);
            }
        };
    }

    /// <summary>
    /// Registers an untyped click handler for a given entity type.
    /// </summary>
    public void RegisterLinkHandler(string entityType, Action<IChronicleEntity, Node> handler)
    {
        if (string.IsNullOrEmpty(entityType) || handler == null)
            return;

        _linkHandlers[entityType] = handler;
    }

    public bool HasLinkHandler(string entityType)
    {
        return !string.IsNullOrEmpty(entityType) && _linkHandlers.ContainsKey(entityType);
    }

    /// <summary>
    /// Registers a tombstone handler for a specific entity type when the live entity no longer exists.
    /// </summary>
    public void RegisterTombstoneHandler(string entityType, Action<ChronicleTombstone, Node> handler)
    {
        if (string.IsNullOrEmpty(entityType) || handler == null)
            return;

        _tombstoneHandlers[entityType] = handler;
    }

    /// <summary>
    /// Registers a fallback tombstone handler invoked for any entity type without a specialized tombstone handler.
    /// </summary>
    public void RegisterDefaultTombstoneHandler(Action<ChronicleTombstone, Node> handler)
    {
        _defaultTombstoneHandler = handler;
    }

    public bool HasDefaultTombstoneHandler => _defaultTombstoneHandler != null;

    /// <summary>
    /// Resolves a chronicle entity by its <c>ChronicleId</c> (or <c>type:id</c> link tag),
    /// pruning freed GodotObject instances safely.
    /// </summary>
    public IChronicleEntity ResolveEntity(string chronicleId)
    {
        if (string.IsNullOrEmpty(chronicleId))
            return null;

        string normalized = chronicleId.StartsWith("entity:", StringComparison.OrdinalIgnoreCase)
            ? chronicleId.Substring("entity:".Length)
            : chronicleId;

        lock (_dbLock)
        {
            if (TryGetValidCachedEntity(normalized, out var cached))
                return cached;

            int colonIdx = normalized.IndexOf(':');
            if (colonIdx > 0)
            {
                string shortId = normalized.Substring(colonIdx + 1);
                if (TryGetValidCachedEntity(shortId, out cached))
                    return cached;
            }
        }

        int typeColonIdx = normalized.IndexOf(':');
        if (typeColonIdx > 0)
        {
            string entityType = normalized.Substring(0, typeColonIdx);
            if (_entityResolvers.TryGetValue(entityType, out var resolver))
            {
                var resolved = resolver(normalized);
                if (IsEntityValid(resolved))
                {
                    RegisterEntity(resolved);
                    return resolved;
                }
            }
        }

        return null;
    }

    private bool TryGetValidCachedEntity(string key, out IChronicleEntity entity)
    {
        if (_entityRegistry.TryGetValue(key, out entity))
        {
            if (IsEntityValid(entity))
                return true;

            _entityRegistry.Remove(key);
            entity = null;
        }
        return false;
    }

    private static bool IsEntityValid(IChronicleEntity entity)
    {
        if (entity == null)
            return false;
        if (entity is GodotObject godotObj)
            return GodotObject.IsInstanceValid(godotObj) && !((godotObj as Node)?.IsQueuedForDeletion() ?? false);
        return true;
    }

    /// <summary>
    /// Parses a BBCode <c>entity:&lt;type&gt;:&lt;id&gt;</c> meta payload, emits <see cref="EntityLinkClicked"/>,
    /// resolves the entity, and invokes the registered handler for its <c>ChronicleType</c>.
    /// </summary>
    public bool TryHandleLinkClick(string meta, Node contextNode = null)
    {
        if (string.IsNullOrEmpty(meta))
            return false;

        string chronicleId = meta.StartsWith("entity:", StringComparison.OrdinalIgnoreCase)
            ? meta.Substring("entity:".Length)
            : meta;

        int colonIdx = chronicleId.IndexOf(':');
        string entityType = colonIdx > 0 ? chronicleId.Substring(0, colonIdx) : string.Empty;

        EmitSignal(SignalName.EntityLinkClicked, entityType, chronicleId);

        IChronicleEntity entity = ResolveEntity(chronicleId);
        if (entity != null)
        {
            string handlerKey = !string.IsNullOrEmpty(entity.ChronicleType) ? entity.ChronicleType : entityType;
            if (_linkHandlers.TryGetValue(handlerKey, out var handler))
            {
                handler(entity, contextNode);
                return true;
            }
            return false;
        }

        // Entity is no longer active in the game — fall back to tombstone snapshot
        ChronicleTombstone tombstone = GetTombstone(chronicleId);
        if (tombstone == null)
            return false;

        string tombstoneKey = !string.IsNullOrEmpty(tombstone.EntityType) ? tombstone.EntityType : entityType;
        EmitSignal(SignalName.TombstoneLinkClicked, tombstoneKey, tombstone.ChronicleId);

        if (_tombstoneHandlers.TryGetValue(tombstoneKey, out var specificTombstoneHandler))
        {
            specificTombstoneHandler(tombstone, contextNode);
            return true;
        }

        if (_defaultTombstoneHandler != null)
        {
            _defaultTombstoneHandler(tombstone, contextNode);
            return true;
        }

        return false;
    }

    public void CloseDatabase()
    {
        lock (_dbLock)
        {
            if (_dbConnection != null)
            {
                _dbConnection.Close();
                _dbConnection.Dispose();
                _dbConnection = null;
            }
            SqliteConnection.ClearAllPools();
        }
    }
}
