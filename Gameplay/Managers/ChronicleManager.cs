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

    [Signal]
    public delegate void EventRecordedEventHandler(long eventId, string title, string category, int tick);

    [Export]
    public string DatabasePath { get; set; } = "res://saves/chronicle.db";

    [Export]
    public bool ResetOnStart { get; set; } = true;

    private SqliteConnection _dbConnection;
    private readonly object _dbLock = new object();

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
        InitializeDatabase(resetDatabase: ResetOnStart);
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
            if (!string.IsNullOrEmpty(customConnectionString))
            {
                connectionString = customConnectionString;
            }
            else
            {
                string globalPath = ProjectSettings.GlobalizePath(DatabasePath);
                string dirPath = System.IO.Path.GetDirectoryName(globalPath);
                if (!string.IsNullOrEmpty(dirPath) && !System.IO.Directory.Exists(dirPath))
                {
                    System.IO.Directory.CreateDirectory(dirPath);
                }

                if (resetDatabase && System.IO.File.Exists(globalPath))
                {
                    try
                    {
                        System.IO.File.Delete(globalPath);
                        string walPath = globalPath + "-wal";
                        string shmPath = globalPath + "-shm";
                        if (System.IO.File.Exists(walPath)) System.IO.File.Delete(walPath);
                        if (System.IO.File.Exists(shmPath)) System.IO.File.Delete(shmPath);
                    }
                    catch (Exception ex)
                    {
                        GD.PushWarning($"Could not delete old chronicle database file: {ex.Message}");
                    }
                }

                connectionString = $"Data Source={globalPath}";
            }

            _dbConnection = new SqliteConnection(connectionString);
            _dbConnection.Open();

            ExecuteSchemaSetup();

            if (resetDatabase)
            {
                ClearHistory();
            }
        }
    }

    private void ExecuteSchemaSetup()
    {
        const string schemaSql = @"
            CREATE TABLE IF NOT EXISTS chronicle_events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                tick INTEGER NOT NULL,
                category TEXT NOT NULL,
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

            const string insertSql = @"
                INSERT INTO chronicle_events (
                    tick, category, title, raw_text, formatted_bbcode, primary_entity_id, entity_tags, metadata_json
                ) VALUES (
                    @tick, @category, @title, @raw_text, @formatted_bbcode, @primary_entity_id, @entity_tags, @metadata_json
                );
                SELECT last_insert_rowid();
            ";

            using var cmd = _dbConnection.CreateCommand();
            cmd.CommandText = insertSql;
            cmd.Parameters.AddWithValue("@tick", record.Tick);
            cmd.Parameters.AddWithValue("@category", record.Category ?? string.Empty);
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
                SELECT id, tick, category, title, raw_text, formatted_bbcode, primary_entity_id, entity_tags, metadata_json
                FROM chronicle_events
                WHERE 1=1
            ");

            using var cmd = _dbConnection.CreateCommand();

            if (!string.IsNullOrEmpty(filter.EntityId))
            {
                queryBuilder.Append(" AND entity_tags LIKE @entity_tag");
                cmd.Parameters.AddWithValue("@entity_tag", $"%,{filter.EntityId},%");
            }

            if (filter.Category.HasValue)
            {
                queryBuilder.Append(" AND category = @category");
                cmd.Parameters.AddWithValue("@category", filter.Category.Value.ToString());
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
                    Title = reader.GetString(3),
                    RawText = reader.GetString(4),
                    FormattedBbcode = reader.GetString(5),
                    PrimaryEntityId = reader.IsDBNull(6) ? string.Empty : reader.GetString(6),
                    EntityTags = reader.GetString(7),
                    MetadataJson = reader.IsDBNull(8) ? "{}" : reader.GetString(8)
                });
            }
        }

        return results;
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

            if (!string.IsNullOrEmpty(filter.EntityId))
            {
                queryBuilder.Append(" AND entity_tags LIKE @entity_tag");
                cmd.Parameters.AddWithValue("@entity_tag", $"%,{filter.EntityId},%");
            }

            if (filter.Category.HasValue)
            {
                queryBuilder.Append(" AND category = @category");
                cmd.Parameters.AddWithValue("@category", filter.Category.Value.ToString());
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
        }
    }
}
