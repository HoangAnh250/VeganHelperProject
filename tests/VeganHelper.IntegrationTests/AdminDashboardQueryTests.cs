using System.Data.Common;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class AdminDashboardQueryTests(Member2ApiFixture fixture)
{
    [Fact]
    public async Task OverviewAndTopCategories_EachUseOneDatabaseRoundTrip()
    {
        var counter = new QueryCounter();
        await using var db = new AppDbContext(new DbContextOptionsBuilder<AppDbContext>()
            .UseNpgsql(fixture.Connection).AddInterceptors(counter).Options);
        var repository = new AdminDashboardRepository(db);
        var start = new DateTime(2000, 1, 1, 0, 0, 0, DateTimeKind.Utc);
        var until = start.AddDays(7);
        var overview = await repository.OverviewAsync(start, until, start.AddDays(-7), default);
        Assert.Equal(1, counter.Readers);
        Assert.True(overview.Members >= 0);
        counter.Readers = 0;
        var categories = await repository.CategoriesAsync(start, until, 3, default);
        Assert.Equal(1, counter.Readers);
        Assert.Equal(0, categories.TotalPosts);
        Assert.Empty(categories.Items);
    }

    private sealed class QueryCounter : DbCommandInterceptor
    {
        public int Readers;
        public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand command, CommandEventData eventData,
            InterceptionResult<DbDataReader> result, CancellationToken cancellationToken = default)
        {
            Readers++;
            return ValueTask.FromResult(result);
        }
    }
}
