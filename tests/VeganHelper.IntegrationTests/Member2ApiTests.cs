using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.IdentityModel.Tokens;
using Npgsql;
using Testcontainers.PostgreSql;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;
using VeganHelper.DAL.Repositories;
using VeganHelper.BLL.Services.Media;
using Microsoft.AspNetCore.Http;

namespace VeganHelper.IntegrationTests;

[CollectionDefinition("Member2 API", DisableParallelization = true)]
public sealed class Member2ApiCollection : ICollectionFixture<Member2ApiFixture> { }

public sealed class Member2ApiFixture : IAsyncLifetime
{
    private PostgreSqlContainer? container;
    public string Connection { get; private set; } = "";
    public Member2ApiFactory Factory { get; private set; } = null!;
    private string? previousSigningKey;
    private string? previousConnection;

    public async Task InitializeAsync()
    {
        Connection = Environment.GetEnvironmentVariable("VEGANHELPER_MEMBER2_TEST_POSTGRES") ?? "";
        if (Connection.Length == 0)
        {
            container = new PostgreSqlBuilder("postgres:17").WithDatabase("member2_tests").Build();
            await container.StartAsync();
            Connection = container.GetConnectionString();
        }
        else
        {
            var configuration = new NpgsqlConnectionStringBuilder(Connection);
            if (configuration.Host is not ("localhost" or "127.0.0.1" or "::1") || !configuration.Database!.StartsWith("member2_test", StringComparison.Ordinal))
                throw new InvalidOperationException("Member2 integration tests require an isolated local test database, never Supabase.");
        }
        await using var db = Context();
        // Model Supabase's browser roles and default grants before running migrations.
        // Otherwise a conditional REVOKE test can pass vacuously on a fresh container.
        await db.Database.ExecuteSqlRawAsync("""
            DO $$ BEGIN
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'anon') THEN
                    CREATE ROLE anon NOLOGIN;
                END IF;
                IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'authenticated') THEN
                    CREATE ROLE authenticated NOLOGIN;
                END IF;
            END $$;
            ALTER DEFAULT PRIVILEGES IN SCHEMA public
                GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO anon, authenticated;
            ALTER DEFAULT PRIVILEGES IN SCHEMA public
                GRANT USAGE, SELECT ON SEQUENCES TO anon, authenticated;
            """);
        await db.Database.MigrateAsync();
        previousSigningKey = Environment.GetEnvironmentVariable("Jwt__SigningKey");
        previousConnection = Environment.GetEnvironmentVariable("ConnectionStrings__DefaultConnection");
        Environment.SetEnvironmentVariable("Jwt__SigningKey", Member2ApiFactory.Key);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", Connection);
        Factory = new Member2ApiFactory(Connection);
    }

    public AppDbContext Context() => new(new DbContextOptionsBuilder<AppDbContext>().UseNpgsql(Connection).Options);

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        Environment.SetEnvironmentVariable("Jwt__SigningKey", previousSigningKey);
        Environment.SetEnvironmentVariable("ConnectionStrings__DefaultConnection", previousConnection);
        if (container is not null) await container.DisposeAsync();
    }
}

public sealed class Member2ApiFactory(string connection) : WebApplicationFactory<Program>
{
    public const string Key = "member2-api-tests-only-signing-key-391-2026-123456789";
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.ConfigureServices(services =>
        {
            // Read-only post tests must not require cloud credentials or accidentally call R2.
            services.RemoveAll<IMediaStorageService>();
            services.AddSingleton<IMediaStorageService, NoCloudMediaStorage>();
            services.RemoveAll<DbContextOptions<AppDbContext>>();
            services.RemoveAll<IDbContextOptionsConfiguration<AppDbContext>>();
            services.RemoveAll<AppDbContext>();
            services.AddDbContext<AppDbContext>(o => o.UseNpgsql(connection));
            services.PostConfigure<VeganHelper.BLL.DTOs.JwtOptions>(options =>
            {
                options.Issuer = "member2-test";
                options.Audience = "member2-test";
            });
            services.PostConfigure<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme, options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true, IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)),
                    ValidateIssuer = true, ValidIssuer = "member2-test", ValidateAudience = true, ValidAudience = "member2-test",
                    ValidateLifetime = true, ClockSkew = TimeSpan.Zero
                };
            });
        });
    }

    public HttpClient Client(long? userId = null, bool expired = false)
    {
        var client = CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new Uri("https://localhost"), AllowAutoRedirect = false });
        if (userId is not null)
        {
            var token = new JwtSecurityToken("member2-test", "member2-test", [new Claim("sub", userId.Value.ToString()), new Claim(ClaimTypes.Role, "member"), new Claim("token_version", "0")],
                notBefore: DateTime.UtcNow.AddHours(-2), expires: expired ? DateTime.UtcNow.AddHours(-1) : DateTime.UtcNow.AddHours(1),
                signingCredentials: new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(Key)), SecurityAlgorithms.HmacSha256));
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", new JwtSecurityTokenHandler().WriteToken(token));
        }
        return client;
    }
    private sealed class NoCloudMediaStorage : IMediaStorageService
    {
        public Task<string> UploadFileAsync(IFormFile file, string folder) => throw new InvalidOperationException("Use an explicit recording storage in upload tests.");
        public Task DeleteFileAsync(string url) => throw new InvalidOperationException("Use an explicit recording storage in delete tests.");
    }
}

[Collection("Member2 API")]
public sealed class Member2ApiTests(Member2ApiFixture fixture)
{
    [Fact]
    public async Task Migration_WhenAddingShopTables_EnablesRlsAndBlocksPublicTablePrivileges()
    {
        await using var connection = new NpgsqlConnection(fixture.Connection);
        await connection.OpenAsync();
        await using var command = new NpgsqlCommand("""
            SELECT COUNT(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
            WHERE n.nspname = 'public' AND c.relname IN ('shop_media','shop_menu_items','shop_opening_periods')
              AND c.relrowsecurity
              AND NOT EXISTS (SELECT 1 FROM aclexplode(COALESCE(c.relacl, acldefault('r', c.relowner))) a WHERE a.grantee = 0)
              AND NOT EXISTS (
                  SELECT 1 FROM pg_roles r WHERE r.rolname IN ('anon','authenticated')
                  AND has_table_privilege(r.oid, c.oid, 'SELECT,INSERT,UPDATE,DELETE'))
            """, connection);

        var protectedTableCount = (long)(await command.ExecuteScalarAsync())!;

        Assert.Equal(3, protectedTableCount);
    }

    private async Task<long> UserAsync()
    {
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var user = new User { Username = "member2_" + suffix, Email = suffix + "@example.invalid", RoleId = 1, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        return user.Id;
    }

    [Theory]
    [InlineData("/api/HealthProfile")]
    [InlineData("/api/HealthProfile/bmi")]
    [InlineData("/api/HealthProfile/bmi-history")]
    [InlineData("/api/HealthProfile/ingredients")]
    [InlineData("/api/posts/1/allergy-warnings")]
    [InlineData("/api/Shops/nearby?lat=10.7&lng=106.7")]
    [InlineData("/api/Shops/search?keyword=chay")]
    [InlineData("/api/Shops/1")]
    public async Task GetMember2Api_WhenGuest_ReturnsUnauthorized(string path)
    {
        using var client = fixture.Factory.Client();

        var result = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task GetHealthProfile_WhenTokenExpired_ReturnsUnauthorized()
    {
        using var client = fixture.Factory.Client(await UserAsync(), expired: true);

        var result = await client.GetAsync("/api/HealthProfile");

        Assert.Equal(HttpStatusCode.Unauthorized, result.StatusCode);
    }

    [Fact]
    public async Task UpdateHealthProfile_WhenValid_PreservesProfileAndIsolatesHistoryByUser()
    {
        var userId = await UserAsync();
        var otherId = await UserAsync();
        await using var db = fixture.Context();
        db.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "Existing name", AvatarUrl = "https://example.invalid/avatar.png", DietType = "vegan" });
        db.BmiHistories.Add(new BmiHistory { UserId = userId, HeightCm = 175, WeightKg = 90, BmiValue = 29.39m, RecordedAt = DateTime.UtcNow.AddMonths(-7) });
        await db.SaveChangesAsync();
        using var client = fixture.Factory.Client(userId);

        var update = await client.PutAsJsonAsync("/api/HealthProfile", new { heightCm = 175, weightKg = 70, birthDate = "1996-01-01", biologicalSex = "MALE", dietType = "VEGAN", activityLevel = "sedentary" });
        var history = await client.GetFromJsonAsync<JsonElement>("/api/HealthProfile/bmi-history");
        using var otherClient = fixture.Factory.Client(otherId);
        var otherHistory = await otherClient.GetFromJsonAsync<JsonElement>("/api/HealthProfile/bmi-history");
        var profile = await db.UserProfiles.AsNoTracking().SingleAsync(p => p.UserId == userId);

        Assert.Equal(HttpStatusCode.OK, update.StatusCode);
        Assert.Equal("Existing name", profile.DisplayName);
        Assert.Equal("https://example.invalid/avatar.png", profile.AvatarUrl);
        Assert.Equal(22.86m, profile.CurrentBmi);
        Assert.Equal(1, history.GetProperty("totalCount").GetInt32());
        Assert.Single(history.GetProperty("history").EnumerateArray());
        Assert.Empty(otherHistory.GetProperty("items").EnumerateArray());
    }

    [Theory]
    [InlineData(99, 70)]
    [InlineData(251, 70)]
    [InlineData(175, 29)]
    [InlineData(175, 201)]
    public async Task UpdateHealthProfile_WhenInvalid_ReturnsBadRequestAndDoesNotWriteHistory(int height, int weight)
    {
        var userId = await UserAsync();
        using var client = fixture.Factory.Client(userId);

        var response = await client.PutAsJsonAsync("/api/HealthProfile", new { heightCm = height, weightKg = weight, birthDate = "1996-01-01", biologicalSex = "male", dietType = "vegan", activityLevel = "sedentary" });
        await using var db = fixture.Context();

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.False(await db.BmiHistories.AnyAsync(h => h.UserId == userId));
    }

    [Fact]
    public async Task DeclareAllergies_WhenReplacingAndInvalidId_PreservesOldSelectionAndRoundTripsCustomNames()
    {
        var userId = await UserAsync();
        using var client = fixture.Factory.Client(userId);
        var name = "Đậu hũ " + Guid.NewGuid().ToString("N");

        var first = await client.PutAsJsonAsync("/api/HealthProfile/allergies", new { allergyIngredientIds = Array.Empty<long>(), customAllergies = new[] { name, name.ToUpperInvariant() } });
        var profile = await client.GetFromJsonAsync<JsonElement>("/api/HealthProfile");
        var invalid = await client.PutAsJsonAsync("/api/HealthProfile/allergies", new { allergyIngredientIds = new[] { long.MaxValue }, customAllergies = new[] { "Must not be saved" } });
        var unchanged = await client.GetFromJsonAsync<JsonElement>("/api/HealthProfile");

        Assert.Equal(HttpStatusCode.OK, first.StatusCode);
        Assert.Equal(name, Assert.Single(profile.GetProperty("customAllergies").EnumerateArray()).GetString());
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        Assert.Equal(name, Assert.Single(unchanged.GetProperty("customAllergies").EnumerateArray()).GetString());

        var cleared = await client.PutAsJsonAsync("/api/HealthProfile/allergies", new { allergyIngredientIds = Array.Empty<long>(), customAllergies = Array.Empty<string>() });
        var empty = await client.GetFromJsonAsync<JsonElement>("/api/HealthProfile");

        Assert.Equal(HttpStatusCode.OK, cleared.StatusCode);
        Assert.Empty(empty.GetProperty("allergies").EnumerateArray());
    }

    [Theory]
    [InlineData("{\"allergyIngredientIds\":null,\"customAllergies\":[]}")]
    [InlineData("{\"allergyIngredientIds\":[],\"customAllergies\":null}")]
    [InlineData("{\"allergyIngredientIds\":[],\"customAllergies\":[null]}")]
    [InlineData("{\"allergyIngredientIds\":[],\"customAllergies\":[\"  \"]}")]
    public async Task DeclareAllergies_WhenBodyContainsNullOrBlankValues_ReturnsBadRequest(string body)
    {
        var userId = await UserAsync();
        using var client = fixture.Factory.Client(userId);
        using var content = new StringContent(body, Encoding.UTF8, "application/json");

        var result = await client.PutAsync("/api/HealthProfile/allergies", content);
        await using var db = fixture.Context();

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
        Assert.False(await db.UserAllergies.AnyAsync(a => a.UserId == userId));
    }

    [Fact]
    public async Task DeclareAllergies_WhenTwoUsersCreateSameCustomNameConcurrently_ReusesOneIngredient()
    {
        var firstId = await UserAsync();
        var secondId = await UserAsync();
        var name = "Đậu mới " + Guid.NewGuid().ToString("N");
        using var first = fixture.Factory.Client(firstId);
        using var second = fixture.Factory.Client(secondId);

        var responses = await Task.WhenAll(
            first.PutAsJsonAsync("/api/HealthProfile/allergies", new { customAllergies = new[] { name } }),
            second.PutAsJsonAsync("/api/HealthProfile/allergies", new { customAllergies = new[] { name.ToUpperInvariant() } }));
        await using var db = fixture.Context();
        var selected = await db.UserAllergies.Where(a => a.UserId == firstId || a.UserId == secondId).ToListAsync();

        Assert.All(responses, response => Assert.Equal(HttpStatusCode.OK, response.StatusCode));
        Assert.Equal(2, selected.Count);
        Assert.Single(selected.Select(a => a.IngredientId).Distinct());
        Assert.All(selected, allergy => Assert.True(allergy.IsCustom));
    }

    [Fact]
    public async Task ReplaceAllergiesAsync_WhenDatabaseRejectsSelection_RollsBackNewIngredient()
    {
        var name = "Rollback ingredient " + Guid.NewGuid().ToString("N");
        await using var db = fixture.Context();
        var repository = new HealthProfileRepository(db);

        var error = await Assert.ThrowsAsync<DbUpdateException>(() =>
            repository.ReplaceAllergiesAsync(long.MaxValue, [], [name], default));
        await using var verify = fixture.Context();

        Assert.Equal(PostgresErrorCodes.ForeignKeyViolation, Assert.IsType<PostgresException>(error.InnerException).SqlState);
        Assert.False(await verify.Ingredients.AnyAsync(i => i.Name == name));
    }

    [Fact]
    public async Task GetBmi_WhenProfileExists_ReturnsCalculationAndNutritionSuggestions()
    {
        var userId = await UserAsync();
        await using var db = fixture.Context();
        db.UserProfiles.Add(new UserProfile { UserId = userId, DisplayName = "BMI test", HeightCm = 175, WeightKg = 70,
            BirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30), BiologicalSex = "male", ActivityLevel = "sedentary", DietType = "vegan" });
        await db.SaveChangesAsync();
        using var client = fixture.Factory.Client(userId);

        var result = await client.GetFromJsonAsync<JsonElement>("/api/HealthProfile/bmi");

        Assert.Equal(22.86m, result.GetProperty("bmi").GetDecimal());
        Assert.Equal("Normal", result.GetProperty("category").GetString());
        Assert.Equal(1978.50m, result.GetProperty("dailyCalorieRecommendation").GetDecimal());
        Assert.NotEmpty(result.GetProperty("nutritionSuggestions").EnumerateArray());
        Assert.StartsWith("https://www.nhs.uk/", result.GetProperty("nutritionSourceUrl").GetString());
    }

    [Fact]
    public async Task GetIngredients_WhenSearching_ReturnsPagedOptionsForAllergyPicker()
    {
        var userId = await UserAsync();
        var keyword = "Nguyên liệu " + Guid.NewGuid().ToString("N");
        await using var db = fixture.Context();
        db.Ingredients.AddRange(new Ingredient { Name = keyword + " A", DefaultUnit = "g" }, new Ingredient { Name = keyword + " B", DefaultUnit = "ml" });
        await db.SaveChangesAsync();
        using var client = fixture.Factory.Client(userId);

        var result = await client.GetFromJsonAsync<JsonElement>("/api/HealthProfile/ingredients?keyword=" + Uri.EscapeDataString(keyword.ToUpperInvariant()) + "&pageIndex=2&pageSize=1");

        Assert.Equal(2, result.GetProperty("totalCount").GetInt32());
        Assert.Equal(2, result.GetProperty("pageNumber").GetInt32());
        Assert.Equal(keyword + " B", Assert.Single(result.GetProperty("items").EnumerateArray()).GetProperty("name").GetString());
    }

    [Fact]
    public async Task GetAllergyWarnings_WhenRecipeContainsSelectedIngredient_ReturnsOnlyCurrentUserWarnings()
    {
        var userId = await UserAsync();
        var otherId = await UserAsync();
        await using var db = fixture.Context();
        var ingredient = new Ingredient { Name = "Peanut " + Guid.NewGuid().ToString("N"), DefaultUnit = "g" };
        db.Ingredients.Add(ingredient);
        var post = new Post { AuthorId = userId, Title = "Allergy check", PostType = "recipe", Status = "published", CreatedAt = DateTime.UtcNow };
        db.Posts.Add(post);
        await db.SaveChangesAsync();
        db.PostIngredients.Add(new PostIngredient { PostId = post.Id, IngredientId = ingredient.Id, Quantity = 1, Unit = "g" });
        db.UserAllergies.Add(new UserAllergy { UserId = userId, IngredientId = ingredient.Id, CreatedAt = DateTime.UtcNow });
        await db.SaveChangesAsync();
        using var client = fixture.Factory.Client(userId);
        using var otherClient = fixture.Factory.Client(otherId);

        var mine = await client.GetFromJsonAsync<JsonElement>($"/api/posts/{post.Id}/allergy-warnings");
        var other = await otherClient.GetFromJsonAsync<JsonElement>($"/api/posts/{post.Id}/allergy-warnings");

        Assert.True(mine.GetProperty("hasAllergyWarning").GetBoolean());
        Assert.Equal(ingredient.Id, Assert.Single(mine.GetProperty("allergens").EnumerateArray()).GetProperty("ingredientId").GetInt64());
        Assert.False(other.GetProperty("hasAllergyWarning").GetBoolean());
    }

    [Fact]
    public async Task SearchAndNearbyShops_WhenUsingRealPostgres_FiltersOrdersAndReturnsDetail()
    {
        var userId = await UserAsync();
        var keyword = "Quán_% " + Guid.NewGuid().ToString("N");
        await using var db = fixture.Context();
        var near = new Shop { Name = keyword, Address = "Hồ Chí Minh", IsApproved = true, Latitude = 10.75m, Longitude = 106.7m, Rating = 4.1m, CreatedAt = DateTime.UtcNow,
            Media = [new ShopMedia { MediaUrl = "https://example.invalid/cover.png", DisplayOrder = 0 }],
            MenuItems = [new ShopMenuItem { Name = "Món " + keyword }],
            OpeningPeriods = [new ShopOpeningPeriod { DayOfWeek = 5, OpensAt = new TimeOnly(8, 0), ClosesAt = new TimeOnly(20, 0) }] };
        var far = new Shop { Name = keyword + " Far", IsApproved = true, Latitude = 21m, Longitude = 105m, Rating = 4.9m, CreatedAt = DateTime.UtcNow };
        var hidden = new Shop { Name = keyword + " Hidden", IsApproved = false, Latitude = 10.75m, Longitude = 106.7m, CreatedAt = DateTime.UtcNow };
        var deleted = new Shop { Name = keyword + " Deleted", IsApproved = true, IsDeleted = true, CreatedAt = DateTime.UtcNow };
        db.Shops.AddRange(near, far, hidden, deleted);
        await db.SaveChangesAsync();
        using var client = fixture.Factory.Client(userId);

        var search = await client.GetFromJsonAsync<JsonElement>("/api/Shops/search?keyword=" + Uri.EscapeDataString(keyword.ToUpperInvariant()) + "&lat=10.75&lng=106.7");
        var nearby = await client.GetFromJsonAsync<JsonElement>("/api/Shops/nearby?lat=10.75&lng=106.7&radiusKm=5");
        var detail = await client.GetFromJsonAsync<JsonElement>($"/api/Shops/{near.Id}?lat=10.75&lng=106.7");
        var unavailable = await client.GetAsync($"/api/Shops/{hidden.Id}");

        Assert.Equal(2, search.GetProperty("totalCount").GetInt32());
        Assert.Equal(far.Id, search.GetProperty("items")[0].GetProperty("shopId").GetInt64());
        Assert.Contains(nearby.GetProperty("items").EnumerateArray(), s => s.GetProperty("shopId").GetInt64() == near.Id && s.GetProperty("distanceKm").GetDouble() == 0);
        Assert.DoesNotContain(nearby.GetProperty("items").EnumerateArray(), s => s.GetProperty("shopId").GetInt64() == far.Id || s.GetProperty("shopId").GetInt64() == hidden.Id);
        Assert.Single(detail.GetProperty("mediaUrls").EnumerateArray());
        Assert.Single(detail.GetProperty("openingPeriods").EnumerateArray());
        Assert.Contains("google.com/maps/dir", detail.GetProperty("googleMapsDirectionsUrl").GetString());
        Assert.Equal(HttpStatusCode.NotFound, unavailable.StatusCode);
    }

    [Fact]
    public async Task SearchShops_WhenKeywordOnlyMatchesMenu_PaginatesAndCalculatesNonzeroDistance()
    {
        var userId = await UserAsync();
        var keyword = "Cơm sen " + Guid.NewGuid().ToString("N");
        await using var db = fixture.Context();
        var closer = new Shop { Name = "Menu search closer", IsApproved = true, Latitude = 10.76m, Longitude = 106.7m, Rating = 4,
            CreatedAt = DateTime.UtcNow, MenuItems = [new ShopMenuItem { Name = keyword }] };
        var farther = new Shop { Name = "Menu search farther", IsApproved = true, Latitude = 10.77m, Longitude = 106.7m, Rating = 4,
            CreatedAt = DateTime.UtcNow, MenuItems = [new ShopMenuItem { Name = keyword }] };
        db.Shops.AddRange(closer, farther);
        await db.SaveChangesAsync();
        using var client = fixture.Factory.Client(userId);
        var url = "/api/Shops/search?keyword=" + Uri.EscapeDataString(keyword.ToUpperInvariant()) + "&lat=10.75&lng=106.7&pageSize=1";

        var first = await client.GetFromJsonAsync<JsonElement>(url);
        var second = await client.GetFromJsonAsync<JsonElement>(url + "&pageIndex=2");

        Assert.Equal(2, first.GetProperty("totalCount").GetInt32());
        Assert.Equal(closer.Id, Assert.Single(first.GetProperty("items").EnumerateArray()).GetProperty("shopId").GetInt64());
        Assert.InRange(first.GetProperty("items")[0].GetProperty("distanceKm").GetDouble(), 1.10, 1.12);
        Assert.Equal(farther.Id, Assert.Single(second.GetProperty("items").EnumerateArray()).GetProperty("shopId").GetInt64());
    }

    [Theory]
    [InlineData("/api/Shops/nearby")]
    [InlineData("/api/Shops/nearby?lat=91&lng=106.7")]
    [InlineData("/api/Shops/nearby?lat=10.7&lng=106.7&radiusKm=50")]
    [InlineData("/api/Shops/search?keyword=a")]
    [InlineData("/api/Shops/search?keyword=chay&lat=10.7")]
    [InlineData("/api/HealthProfile/bmi-history?pageIndex=0")]
    [InlineData("/api/HealthProfile/bmi")]
    [InlineData("/api/Shops/nearby?lat=NaN&lng=106.7")]
    [InlineData("/api/Shops/search?keyword=chay&pageSize=101")]
    public async Task GetMember2Api_WhenRequestIsInvalid_ReturnsBadRequest(string path)
    {
        using var client = fixture.Factory.Client(await UserAsync());

        var result = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.BadRequest, result.StatusCode);
    }
}
