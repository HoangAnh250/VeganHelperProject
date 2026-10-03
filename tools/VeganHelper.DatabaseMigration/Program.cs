using System.Data;
using System.Text.Json;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using Npgsql;
using NpgsqlTypes;
using VeganHelper.DAL.Persistence;

// Only this offline tool knows about SQL Server. The application uses PostgreSQL exclusively.
try
{
    var command = args.FirstOrDefault() ?? "help";
    var file = Path.GetFullPath(args.ElementAtOrDefault(1) ?? ".local-data/sqlserver-export.json");
    var configPath = DatabaseConnection.FindLocalConfiguration(Directory.GetCurrentDirectory())
        ?? throw new InvalidOperationException("Create supabase.local.json from supabase.example.json first.");
    using var config = JsonDocument.Parse(await File.ReadAllTextAsync(configPath));
    var configuredTarget = config.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
    var targetConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection") ?? configuredTarget;
    // Export/schema inspection can run before a Supabase project exists. Never open this placeholder.
    var modelConnection = string.IsNullOrWhiteSpace(targetConnection)
        ? "Host=localhost;Database=veganhelper_schema;Username=postgres" : DatabaseConnection.Validate(targetConnection);
    await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(modelConnection).Options);
    var tables = GetTables(db.Model);

    if (command == "check")
    {
        DatabaseConnection.Validate(targetConnection);
        await db.Database.OpenConnectionAsync();
        var connection = (NpgsqlConnection)db.Database.GetDbConnection();
        Console.WriteLine($"PostgreSQL connection verified. Server version: {connection.PostgreSqlVersion}. Host: {connection.Host}.");
        var pending = await db.Database.GetPendingMigrationsAsync();
        Console.WriteLine("Pending application migrations: " + string.Join(", ", pending));
    }
    else if (command == "export")
    {
        var sourceConnection = config.RootElement.GetProperty("Migration").GetProperty("SourceConnectionString").GetString()
            ?? throw new InvalidOperationException("Migration.SourceConnectionString is missing.");
        await using var source = new SqlConnection(sourceConnection);
        await source.OpenAsync();
        // Consistent snapshot without modifying the source. Stop local API writes while exporting.
        await using var transaction = (SqlTransaction)await source.BeginTransactionAsync(IsolationLevel.Serializable);
        await ValidateSourceTables(source, transaction, tables);
        var exported = new List<TableSnapshot>();
        foreach (var table in tables)
        {
            await using var query = new SqlCommand($"SELECT {string.Join(",", table.Columns.Select(c => SqlQuote(c.Name)))} FROM [dbo].{SqlQuote(table.Name)} ORDER BY {string.Join(",", table.Keys.Select(SqlQuote))}", source, transaction) { CommandTimeout = 300 };
            await using var reader = await query.ExecuteReaderAsync();
            var rows = new List<JsonElement[]>();
            while (await reader.ReadAsync())
            {
                var row = new JsonElement[table.Columns.Count];
                for (var index = 0; index < row.Length; index++)
                {
                    object? value = reader.IsDBNull(index) ? null : reader.GetValue(index);
                    if (value is DateTime timestamp && table.Columns[index].StoreType == "timestamp with time zone")
                        value = DateTime.SpecifyKind(timestamp, DateTimeKind.Utc);
                    if (value is DateTime date && table.Columns[index].StoreType == "date")
                        value = DateOnly.FromDateTime(date);
                    row[index] = JsonSerializer.SerializeToElement(value, value?.GetType() ?? typeof(object));
                }
                rows.Add(row);
            }
            exported.Add(new(table.Name, table.Columns, rows));
            Console.WriteLine($"Export {table.Name}: {rows.Count} rows");
        }
        await transaction.CommitAsync();
        Directory.CreateDirectory(Path.GetDirectoryName(file)!);
        // Never overwrite an existing export; it is also the rollback/source backup artifact.
        await using var output = new FileStream(file, FileMode.CreateNew);
        await JsonSerializer.SerializeAsync(output, new DatabaseSnapshot(1, DateTime.UtcNow, exported));
        Console.WriteLine($"Export complete: {file}. SQL Server was not modified.");
    }
    else if (command is "import" or "verify" or "verify-preserved")
    {
        DatabaseConnection.Validate(targetConnection);
        await using var input = File.OpenRead(file);
        var snapshot = await JsonSerializer.DeserializeAsync<DatabaseSnapshot>(input)
            ?? throw new InvalidOperationException("Invalid export file.");
        ValidateSnapshot(snapshot, tables);
        if (command == "import") await db.Database.MigrateAsync();
        await db.Database.OpenConnectionAsync();
        var target = (NpgsqlConnection)db.Database.GetDbConnection();
        await using var transaction = await target.BeginTransactionAsync(command == "import" ? IsolationLevel.ReadCommitted : IsolationLevel.RepeatableRead);
        if (command == "import")
        {
            await Execute(target, transaction, "SELECT pg_advisory_xact_lock(3912026)");
            await Execute(target, transaction, "LOCK TABLE " + string.Join(",", tables.Select(t => PgTable(t.Name))) + " IN ACCESS EXCLUSIVE MODE");
            await EnsureEmptyTarget(target, transaction, tables);
            await Execute(target, transaction, "SET CONSTRAINTS ALL DEFERRED");
            // The EF baseline inserts the two roles. Replace only this known bootstrap data.
            await Execute(target, transaction, "DELETE FROM public.roles");
            foreach (var table in snapshot.Tables)
            {
                await using var writer = await target.BeginBinaryImportAsync($"COPY {PgTable(table.Name)} ({string.Join(",", table.Columns.Select(c => PgQuote(c.Name)))}) FROM STDIN (FORMAT BINARY)");
                foreach (var row in table.Rows)
                {
                    await writer.StartRowAsync();
                    for (var index = 0; index < row.Length; index++)
                    {
                        if (row[index].ValueKind == JsonValueKind.Null) await writer.WriteNullAsync();
                        else await writer.WriteAsync(ReadValue(row[index], table.Columns[index].StoreType), GetDbType(table.Columns[index].StoreType));
                    }
                }
                await writer.CompleteAsync();
                Console.WriteLine($"Import {table.Name}: {table.Rows.Count} rows");
            }
            // Validate all deferred foreign keys before committing or changing sequences.
            await Execute(target, transaction, "SET CONSTRAINTS ALL IMMEDIATE");
        }
        await Verify(target, transaction, snapshot, tables, allowAdditionalRows: command == "verify-preserved");
        if (command == "import")
        {
            foreach (var table in tables)
            foreach (var column in table.Columns.Where(c => c.Identity))
            {
                await using var reset = new NpgsqlCommand($"SELECT setval(pg_get_serial_sequence(@table, @column), COALESCE((SELECT MAX({PgQuote(column.Name)}) FROM {PgTable(table.Name)}), 1), EXISTS (SELECT 1 FROM {PgTable(table.Name)}))", target, transaction);
                reset.Parameters.AddWithValue("table", PgTable(table.Name));
                reset.Parameters.AddWithValue("column", column.Name);
                await reset.ExecuteNonQueryAsync();
            }
        }
        await transaction.CommitAsync();
        Console.WriteLine(command == "import" ? "Import committed. Every table and field matched the export."
            : command == "verify-preserved" ? "Verification passed. Every original row and field was preserved; additional rows are allowed."
            : "Verification passed. Every table and field matched the export.");
    }
    else
    {
        Console.WriteLine("Usage: dotnet run --project tools/VeganHelper.DatabaseMigration -- check|export|import|verify|verify-preserved [snapshot.json]");
    }
    return 0;
}
catch (Exception exception)
{
    // Provider errors can contain private row values. Report metadata only, never row payloads/credentials.
    if (exception is PostgresException postgres)
    {
        Console.Error.WriteLine($"PostgreSQL operation failed: SQLSTATE={postgres.SqlState}; table={postgres.TableName}; column={postgres.ColumnName}; constraint={postgres.ConstraintName}. Import data was not committed.");
        if (postgres.SqlState is "42883" or "42601" or "42703" or "42704")
            Console.Error.WriteLine("Schema error: " + postgres.MessageText);
    }
    else if (exception is SqlException sql)
        Console.Error.WriteLine($"SQL Server operation failed: error {sql.Number}. Check source connection and schema. No source data was changed.");
    else if (exception is NpgsqlException)
    {
        var cause = exception.GetBaseException();
        var code = cause is System.Net.Sockets.SocketException socket ? socket.SocketErrorCode.ToString() : cause.GetType().Name;
        Console.Error.WriteLine($"PostgreSQL connection failed ({code}). Check host, username, password, SSL and network in supabase.local.json. No credentials were logged.");
        if (cause is System.Security.Authentication.AuthenticationException)
            Console.Error.WriteLine("TLS validation: " + cause.Message);
    }
    else Console.Error.WriteLine(exception.Message);
    return 1;
}

static List<TableDefinition> GetTables(IModel model) => model.GetEntityTypes()
    .OrderBy(e => e.GetTableName(), StringComparer.Ordinal)
    .Select(e =>
    {
        var store = StoreObjectIdentifier.Table(e.GetTableName()!, e.GetSchema());
        var columns = e.GetProperties().OrderBy(p => p.GetColumnName(store), StringComparer.Ordinal)
            .Select(p => new ColumnDefinition(p.GetColumnName(store)!, p.GetColumnType()!, p.GetValueGenerationStrategy() == Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn)).ToList();
        var keys = e.FindPrimaryKey()!.Properties.Select(p => p.GetColumnName(store)!).ToList();
        return new TableDefinition(e.GetTableName()!, columns, keys);
    }).ToList();

static async Task ValidateSourceTables(SqlConnection source, SqlTransaction transaction, List<TableDefinition> tables)
{
    await using var command = new SqlCommand("SELECT TABLE_NAME, COLUMN_NAME FROM INFORMATION_SCHEMA.COLUMNS WHERE TABLE_SCHEMA = 'dbo' AND TABLE_NAME <> '__EFMigrationsHistory' ORDER BY TABLE_NAME, ORDINAL_POSITION", source, transaction);
    await using var reader = await command.ExecuteReaderAsync();
    var sourceColumns = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
    while (await reader.ReadAsync())
    {
        var name = reader.GetString(0);
        if (!sourceColumns.TryGetValue(name, out var columns)) sourceColumns[name] = columns = new(StringComparer.Ordinal);
        columns.Add(reader.GetString(1));
    }
    if (!sourceColumns.Keys.ToHashSet().SetEquals(tables.Select(t => t.Name)))
        throw new InvalidOperationException("Source tables differ from the application model. Migration stopped to avoid omitting data: " + string.Join(",", sourceColumns.Keys.Except(tables.Select(t => t.Name))));
    foreach (var table in tables)
        if (!sourceColumns[table.Name].SetEquals(table.Columns.Select(c => c.Name)))
            throw new InvalidOperationException($"Source columns differ for {table.Name}. Migration stopped to avoid omitting data.");
}

static void ValidateSnapshot(DatabaseSnapshot snapshot, List<TableDefinition> tables)
{
    if (snapshot.Version != 1 || snapshot.Tables.Count != tables.Count || snapshot.Tables.Select(t => t.Name).Distinct().Count() != tables.Count)
        throw new InvalidOperationException("Export version/table list does not match the application model.");
    foreach (var table in tables)
    {
        var exported = snapshot.Tables.SingleOrDefault(t => t.Name == table.Name);
        if (exported is null || !exported.Columns.SequenceEqual(table.Columns) || exported.Rows.Any(r => r.Length != table.Columns.Count))
            throw new InvalidOperationException($"Export schema does not match {table.Name}.");
    }
}

static async Task EnsureEmptyTarget(NpgsqlConnection target, NpgsqlTransaction transaction, List<TableDefinition> tables)
{
    foreach (var table in tables)
    {
        var sql = table.Name == "roles"
            ? "SELECT COUNT(*) FROM public.roles WHERE NOT ((id = 1 AND role_name = 'member') OR (id = 2 AND role_name = 'admin'))"
            : $"SELECT COUNT(*) FROM {PgTable(table.Name)}";
        await using var query = new NpgsqlCommand(sql, target, transaction);
        if (Convert.ToInt64(await query.ExecuteScalarAsync()) != 0)
            throw new InvalidOperationException($"Target table {table.Name} is not empty. Import refused; existing target data was preserved.");
    }
}

static async Task Verify(NpgsqlConnection target, NpgsqlTransaction transaction, DatabaseSnapshot snapshot, List<TableDefinition> tables, bool allowAdditionalRows = false)
{
    foreach (var table in tables)
    {
        var expected = snapshot.Tables.Single(t => t.Name == table.Name);
        var keyIndexes = table.Keys.Select(k => table.Columns.FindIndex(c => c.Name == k)).ToArray();
        var expectedByKey = expected.Rows.ToDictionary(
            row => JsonSerializer.Serialize(keyIndexes.Select(i => ReadValue(row[i], table.Columns[i].StoreType)).ToArray()));
        await using var query = new NpgsqlCommand($"SELECT {string.Join(",", table.Columns.Select(c => PgQuote(c.Name)))} FROM {PgTable(table.Name)} ORDER BY {string.Join(",", table.Keys.Select(PgQuote))}", target, transaction) { CommandTimeout = 300 };
        await using var reader = await query.ExecuteReaderAsync();
        var rowIndex = 0;
        var additionalRows = 0;
        while (await reader.ReadAsync())
        {
            var key = JsonSerializer.Serialize(keyIndexes.Select(reader.GetValue).ToArray());
            if (!expectedByKey.Remove(key, out var expectedRow))
            {
                if (!allowAdditionalRows) throw new InvalidOperationException($"Unexpected row: {table.Name}");
                additionalRows++;
                continue;
            }
            for (var index = 0; index < table.Columns.Count; index++)
            {
                var column = table.Columns[index];
                var source = expectedRow[index];
                var actual = reader.IsDBNull(index) ? null
                    : column.StoreType == "date" ? (object)reader.GetFieldValue<DateOnly>(index)
                    : reader.GetValue(index);
                var wanted = source.ValueKind == JsonValueKind.Null ? null : ReadValue(source, column.StoreType);
                var matches = column.StoreType == "jsonb" && actual is string actualJson && wanted is string wantedJson
                    ? System.Text.Json.Nodes.JsonNode.DeepEquals(System.Text.Json.Nodes.JsonNode.Parse(actualJson), System.Text.Json.Nodes.JsonNode.Parse(wantedJson))
                    : Equals(actual, wanted);
                if (!matches) throw new InvalidOperationException($"Data mismatch: table={table.Name}; row={rowIndex + 1}; column={column.Name}. No private values were logged.");
            }
            rowIndex++;
        }
        if (expectedByKey.Count != 0) throw new InvalidOperationException($"Original rows missing: {table.Name}");
        Console.WriteLine($"Verify {table.Name}: {rowIndex} original rows, all fields match; {additionalRows} additional rows");
    }
}

static object ReadValue(JsonElement value, string type) => type switch
{
    "bigint" => value.GetInt64(),
    "integer" => value.GetInt32(),
    "smallint" => value.GetInt16(),
    "boolean" => value.GetBoolean(),
    "uuid" => value.GetGuid(),
    // PostgreSQL timestamps store microseconds; keep original 100ns precision in the source export.
    "timestamp with time zone" => new DateTime(value.GetDateTime().ToUniversalTime().Ticks / 10 * 10, DateTimeKind.Utc),
    "date" => DateOnly.Parse(value.GetString()!, System.Globalization.CultureInfo.InvariantCulture),
    _ when type.StartsWith("numeric", StringComparison.Ordinal) => value.GetDecimal(),
    _ => value.GetString()!
};

static NpgsqlDbType GetDbType(string type) => type switch
{
    "bigint" => NpgsqlDbType.Bigint,
    "integer" => NpgsqlDbType.Integer,
    "smallint" => NpgsqlDbType.Smallint,
    "boolean" => NpgsqlDbType.Boolean,
    "uuid" => NpgsqlDbType.Uuid,
    "timestamp with time zone" => NpgsqlDbType.TimestampTz,
    "date" => NpgsqlDbType.Date,
    "jsonb" => NpgsqlDbType.Jsonb,
    _ when type.StartsWith("numeric", StringComparison.Ordinal) => NpgsqlDbType.Numeric,
    _ => NpgsqlDbType.Text
};

static string SqlQuote(string value) => "[" + value.Replace("]", "]]") + "]";
static string PgQuote(string value) => "\"" + value.Replace("\"", "\"\"") + "\"";
static string PgTable(string name) => "public." + PgQuote(name);
static async Task Execute(NpgsqlConnection connection, NpgsqlTransaction transaction, string sql)
{
    await using var command = new NpgsqlCommand(sql, connection, transaction) { CommandTimeout = 300 };
    await command.ExecuteNonQueryAsync();
}

record ColumnDefinition(string Name, string StoreType, bool Identity);
record TableDefinition(string Name, List<ColumnDefinition> Columns, List<string> Keys);
record TableSnapshot(string Name, List<ColumnDefinition> Columns, List<JsonElement[]> Rows);
record DatabaseSnapshot(int Version, DateTime ExportedAtUtc, List<TableSnapshot> Tables);
