using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata;
using Microsoft.EntityFrameworkCore.Migrations;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.UnitTests;

public sealed class NotificationModelTests
{
    [Fact]
    public void PostgreSqlModel_MatchesMigrationSnapshot()
    {
        using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
         .UseNpgsql("Host=127.0.0.1;Database=unused;Username=postgres").Options);
        var snapshot = db.GetService<IMigrationsAssembly>().ModelSnapshot!.Model;
        snapshot = db.GetService<IModelRuntimeInitializer>().Initialize(snapshot, designTime: true);
        var current = db.GetService<IDesignTimeModel>().Model;
        var differences = db.GetService<IMigrationsModelDiffer>().GetDifferences(snapshot.GetRelationalModel(), current.GetRelationalModel());
        var details = string.Join("\n", differences.Select(op => op.GetType().Name + " " + string.Join(" ",
         new[] { "Table", "Name", "Sql" }.Select(p => op.GetType().GetProperty(p)?.GetValue(op)?.ToString()))));
        Assert.True(differences.Count == 0, details);
    }
}
