using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace VeganHelper.DAL.Persistence;

public sealed class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    public AppDbContext CreateDbContext(string[] args)
    {
        var connectionString = Environment.GetEnvironmentVariable("VeganHelper_DefaultConnection")
            ?? "Server=(localdb)\\MSSQLLocalDB;Database=VeganHelperSystem;Trusted_Connection=True;TrustServerCertificate=True;Connect Timeout=3";
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer(connectionString)
            .Options;
        return new AppDbContext(options);
    }
}
