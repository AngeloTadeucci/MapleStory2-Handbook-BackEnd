using System.Data;
using GameParser;
using GameParser.Parsers;
using Maple2Storage.Tools;
using Maple2Storage.Types;
using MySql.Data.MySqlClient;
using SqlKata.Execution;

DotEnv.Load();

// Ask if user wants to save view counts before parsing (must be done BEFORE dropping database)
Dictionary<string, Dictionary<int, int>>? viewCounts = null;
if (DatabaseExists()) {
    Console.WriteLine("Do you want to save current view counts before parsing? (y/n)");
    if (Console.ReadLine()?.ToLower() == "y") {
        Console.WriteLine("Saving view counts...");
        viewCounts = GetViewCount();
        Console.WriteLine($"View counts saved in memory ({viewCounts.Sum(t => t.Value.Count)} records)");
    }
}

Console.WriteLine("View count:");
foreach (var (table, counts) in viewCounts ?? new()) {
    Console.WriteLine($" - {table}: {counts.Count} records");
}

// check if database exists
if (!DatabaseExists()) {
    Console.WriteLine("Database does not exist. Creating...");
    CreateDatabase();
} else {
    Console.WriteLine("Do you want to drop and create the whole database? (y/n)");
    if (Console.ReadLine()?.ToLower() == "y") {
        Console.WriteLine("Clearing database...");
        CreateDatabase();
    }
}

// Define table groups: each group has a list of tables and a single parser that populates all of them
(string[] tables, Action parser)[] tableGroups = [
    (["items"], ItemParser.Parse),
    (["item_boxes"], ItemDropParser.Parse),
    (["npcs"], NpcParser.Parse),
    (["maps", "map_npcs", "map_portals", "map_mobs"], () => {
        MapNameParser.Parse();
        MapSpawnParser.Parse();  // Parse spawn metadata (requires NpcParser to run first for tag lookup)
        MapEntityParser.Parse();
    }),
    (["achieves"], AchieveParser.Parse),
    (["additional_effects"], AdditionalEffectParser.Parse),
    (["quests", "quest_maps"], QuestParser.Parse),
];

foreach ((string[] tables, Action parser) in tableGroups) {
    string mainTable = tables[0];
    bool allTablesExist = tables.All(TableExists);

    // If any table in the group doesn't exist, create all and run parser
    if (!allTablesExist) {
        Console.WriteLine($"{mainTable} table group does not exist. Creating {string.Join(", ", tables)}...");
        foreach (string table in tables) {
            DropAndCreateTable(table);
        }
        parser();
        continue;
    }

    // Ask user about the main table
    Console.WriteLine($"Drop and create {mainTable}" + (tables.Length > 1 ? $" (+ {string.Join(", ", tables.Skip(1))})" : "") + "? (y/n)");
    if (Console.ReadLine()?.ToLower() == "n") {
        continue;
    }

    // Drop and create all tables in the group
    foreach (string table in tables) {
        DropAndCreateTable(table);
    }
    parser();
}

Console.WriteLine("Finished parsing!");

// Restore view counts if they were saved
if (viewCounts != null) {
    Console.WriteLine("Restoring view counts...");
    RestoreViewCount(viewCounts);
    Console.WriteLine("View counts restored!");
}

Console.WriteLine("Finished!");

static void CreateDatabase() {
    string databaseSql = File.ReadAllText(Path.Combine(Paths.SolutionDir, "GameParser", "SQL", "database.sql"));
    string databaseName = Environment.GetEnvironmentVariable("DB_NAME")!;
    MySqlScript script = new(QueryManager.ConnectionNoDb(), databaseSql.Replace("{databaseName}", databaseName));
    script.Execute();
}

static bool DatabaseExists() {
    // Obtain the connection object
    var connection = QueryManager.ConnectionNoDb();

    string? databaseName = Environment.GetEnvironmentVariable("DB_NAME");

    // Check if the database name is not null or empty
    if (string.IsNullOrEmpty(databaseName)) {
        throw new NullReferenceException("Database name is null or empty");
    }

    // Define the query to check if the database exists
    string query = $"SELECT SCHEMA_NAME FROM INFORMATION_SCHEMA.SCHEMATA WHERE SCHEMA_NAME = '{databaseName}'";

    // Create the MySqlCommand object
    using (MySqlCommand command = new(query, connection)) {
        // Ensure the connection is open
        if (connection.State == ConnectionState.Closed) {
            connection.Open();
        }

        // Execute the query and check if the result is not null
        return command.ExecuteScalar() != null;
    }
}

static void DropAndCreateTable(string tableName) {
    string databaseSql = File.ReadAllText(Path.Combine(Paths.SolutionDir, "GameParser", "SQL", $"{tableName}.sql"));
    string databaseName = Environment.GetEnvironmentVariable("DB_NAME")!;

    MySqlScript script = new(QueryManager.Connection(), databaseSql.Replace("{databaseName}", databaseName));
    script.Execute();
}

static bool TableExists(string tableName) {
    var connection = QueryManager.ConnectionNoDb();

    string? databaseName = Environment.GetEnvironmentVariable("DB_NAME");

    string query = $"SELECT table_name FROM information_schema.tables WHERE table_schema = '{databaseName}' AND table_name = '{tableName}'";
    // Create the MySqlCommand object
    using (MySqlCommand command = new(query, connection)) {
        // Ensure the connection is open
        if (connection.State == ConnectionState.Closed) {
            connection.Open();
        }

        // Execute the query and check if the result is not null
        return command.ExecuteScalar() != null;
    }
}

static Dictionary<string, Dictionary<int, int>> GetViewCount() {
    var viewCounts = new Dictionary<string, Dictionary<int, int>>();
    string[] tablesToUpdate = ["npcs", "items", "maps", "achieves", "quests"];

    foreach (string tableName in tablesToUpdate) {
        if (TableExists(tableName)) {
            Console.WriteLine($"Getting view counts for {tableName}...");
            var results = QueryManager.QueryFactory.Query(tableName)
                .Where("visit_count", ">", 0)
                .Select("id", "visit_count")
                .Get();

            var tableViewCounts = new Dictionary<int, int>();
            foreach (dynamic result in results) {
                tableViewCounts[(int) result.id] = (int) result.visit_count;
            }

            if (tableViewCounts.Count > 0) {
                viewCounts[tableName] = tableViewCounts;
            }
        }
    }

    return viewCounts;
}

static void RestoreViewCount(Dictionary<string, Dictionary<int, int>> viewCounts) {
    foreach (var (tableName, counts) in viewCounts) {
        if (!TableExists(tableName)) {
            Console.WriteLine($"Table {tableName} does not exist. Skipping view count restoration for this table.");
            continue;
        }

        Console.WriteLine($"Restoring view counts for {tableName}... ({counts.Count} records)");

        foreach (var (id, visitCount) in counts) {
            QueryManager.QueryFactory.Query(tableName)
                .Where("id", id)
                .Update(new { visit_count = visitCount });
        }
    }
}
