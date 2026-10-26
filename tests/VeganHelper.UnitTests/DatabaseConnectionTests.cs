using Npgsql;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.UnitTests;

public sealed class DatabaseConnectionTests
{
    [Theory]
    [InlineData("Host=localhost;Port=5432;Database=postgres;Username=test;Password=test;Maximum Pool Size=80")]
    [InlineData("postgresql://postgres.project:test@localhost:5432/postgres")]
    public void Validate_SetsMaximumPoolSizeToFifteen(string connectionString)
    {
        var validated = DatabaseConnection.Validate(connectionString);
        var builder = new NpgsqlConnectionStringBuilder(validated);

        Assert.Equal(15, builder.MaxPoolSize);
    }
}
