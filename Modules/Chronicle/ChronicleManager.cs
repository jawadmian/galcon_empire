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
