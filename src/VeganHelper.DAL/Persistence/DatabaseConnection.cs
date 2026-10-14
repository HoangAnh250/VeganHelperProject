using System.Text.Json;
using Npgsql;

namespace VeganHelper.DAL.Persistence;

public static class DatabaseConnection
{
    public static string? FindLocalConfiguration(string startDirectory)
    {
        for (var directory = new DirectoryInfo(startDirectory); directory is not null; directory = directory.Parent)
        {
            var path = Path.Combine(directory.FullName, "supabase.local.json");
            if (File.Exists(path)) return path;
        }
        return null;
    }

    public static string ReadPostgresConnection()
    {
        var connection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection")
            ?? Environment.GetEnvironmentVariable("VeganHelper_DefaultConnection");
        if (string.IsNullOrWhiteSpace(connection))
        {
            var path = FindLocalConfiguration(Directory.GetCurrentDirectory());
            if (path is not null)
            {
                using var document = JsonDocument.Parse(File.ReadAllText(path));
                connection = document.RootElement.GetProperty("ConnectionStrings").GetProperty("DefaultConnection").GetString();
            }
        }
        return Validate(connection);
    }

    public static string Validate(string? connection)
    {
        if (string.IsNullOrWhiteSpace(connection))
            throw new InvalidOperationException("Configure the Supabase PostgreSQL connection in supabase.local.json or ConnectionStrings__DefaultConnection. See docs/SUPABASE_SETUP_VI.md.");
        try
        {
            if (connection.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase)
                || connection.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
            {
                // Supabase's copied URI may contain unescaped punctuation in the password.
                // Parse the host separately so '#'/'?' in credentials cannot truncate user-info.
                var separator = connection.LastIndexOf('@');
                var schemeEnd = connection.IndexOf("://", StringComparison.Ordinal) + 3;
                if (separator < schemeEnd) throw new ArgumentException();
                var uri = new Uri("postgresql://" + connection[(separator + 1)..]);
                var credentials = connection[schemeEnd..separator].Split(':', 2);
                if (credentials.Length != 2) throw new ArgumentException();
                connection = new NpgsqlConnectionStringBuilder
                {
                    Host = uri.Host,
                    Port = uri.IsDefaultPort ? 5432 : uri.Port,
                    Database = Uri.UnescapeDataString(uri.AbsolutePath.TrimStart('/')),
                    Username = Uri.UnescapeDataString(credentials[0]),
                    Password = Uri.UnescapeDataString(credentials[1]),
                    SslMode = SslMode.VerifyFull,
                    Timeout = 30,
                    CommandTimeout = 120
                }.ConnectionString;
            }
            var builder = new NpgsqlConnectionStringBuilder(connection);
            if (string.IsNullOrWhiteSpace(builder.Host) || string.IsNullOrWhiteSpace(builder.Database))
                throw new ArgumentException();
            if (builder.Host.EndsWith(".supabase.com", StringComparison.OrdinalIgnoreCase)
                || builder.Host.EndsWith(".supabase.co", StringComparison.OrdinalIgnoreCase))
            {
                builder.SslMode = SslMode.VerifyFull;
                if (string.IsNullOrWhiteSpace(builder.RootCertificate))
                {
                    var bundledCertificate = Path.Combine(AppContext.BaseDirectory, "supabase-root-2021.crt");
                    if (File.Exists(bundledCertificate)) builder.RootCertificate = bundledCertificate;
                    for (var directory = new DirectoryInfo(AppContext.BaseDirectory); directory is not null; directory = directory.Parent)
                    {
                        var certificate = Path.Combine(directory.FullName, "docs", "certificates", "supabase-root-2021.crt");
                        if (!File.Exists(certificate)) continue;
                        builder.RootCertificate = certificate;
                        break;
                    }
                }
            }
            connection = builder.ConnectionString;
        }
        catch (Exception exception) when (exception is ArgumentException or UriFormatException)
        {
            throw new InvalidOperationException("DefaultConnection must be a PostgreSQL/Npgsql connection string (Host=...;Database=...;Username=...;Password=...). SQL Server connection strings are no longer supported.");
        }
        return connection;
    }
}
