using System.Security.Cryptography;
using System.Text;
using FluentValidation;
using Microsoft.OpenApi.Models;
using Microsoft.EntityFrameworkCore;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.BLL.Services;
using VeganHelper.BLL.Services.Media;
using VeganHelper.DAL.Persistence;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using VeganHelper.API.Infrastructure.Email;
using VeganHelper.API.Infrastructure.Google;
using VeganHelper.API.Middlewares;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.DAL.DependencyInjection;
using VeganHelper.DAL.Repositories;
using VeganHelper.DAL.Storage;
using VeganHelper.BLL.Mapping;
using Microsoft.Extensions.Options;
using Hangfire;
using Hangfire.PostgreSql;
using VeganHelper.API.BackgroundJobs;
using VeganHelper.API.Infrastructure.Notifications;
using VeganHelper.DAL.Integrations;

if (args.Contains("--generate-vapid-keys"))
{
    using var vapidKey = ECDsa.Create(ECCurve.NamedCurves.nistP256);
    var parts = vapidKey.ExportParameters(true);
    var point = new byte[65]; point[0] = 4;
    parts.Q.X!.CopyTo(point, 1); parts.Q.Y!.CopyTo(point, 33);
    static string Encode(byte[] bytes) => Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    Console.WriteLine(System.Text.Json.JsonSerializer.Serialize(new { PublicKey = Encode(point), PrivateKey = Encode(parts.D!) }));
    return;
}

var builder = WebApplication.CreateBuilder(args);

var supabaseConfigPath = DatabaseConnection.FindLocalConfiguration(builder.Environment.ContentRootPath);
if (supabaseConfigPath is not null)
    builder.Configuration.AddJsonFile(supabaseConfigPath, optional: false, reloadOnChange: false);
builder.Configuration.AddEnvironmentVariables();
var envFilePath = FindEnvironmentFile(builder.Environment.ContentRootPath);
if (envFilePath is not null)
{
    DotNetEnv.Env.Load(envFilePath);
    builder.Configuration.AddEnvironmentVariables();
}

builder.Services.AddControllers();
var allowedOrigins = builder.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>()
    ?? ["http://localhost:3000"];
builder.Services.AddCors(options =>
{
    options.AddPolicy("Frontend", policy =>
    {
        policy
            .WithOrigins(allowedOrigins)
            .AllowAnyHeader()
            .AllowAnyMethod();
    });
});
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Name = "Authorization",
        Type = SecuritySchemeType.Http,
        Scheme = "bearer",
        BearerFormat = "JWT",
        In = ParameterLocation.Header,
        Description = "Enter a valid JWT access token."
    });
    options.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

builder.Services.AddDal(builder.Configuration);
builder.Services.AddScoped<IStatusService, StatusService>();
builder.Services.AddScoped<IPostRepository, PostRepository>();
builder.Services.AddScoped<IPostService, PostService>();
builder.Services.AddScoped<ICategoryService, CategoryService>();
builder.Services.AddScoped<IPostInteractionRepository, PostInteractionRepository>();
builder.Services.AddScoped<IPostInteractionService, PostInteractionService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreatePostRequestValidator>();

builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.AddScoped<IEmailSender, SendGridEmailSender>();
builder.Services.AddScoped<IGoogleTokenValidator, GoogleTokenValidator>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));
builder.Services.Configure<SendGridOptions>(builder.Configuration.GetSection("SendGrid"));
builder.Services.Configure<GoogleOptions>(builder.Configuration.GetSection("Google"));

var jwtOptions = builder.Configuration.GetSection("Jwt").Get<JwtOptions>()
    ?? throw new InvalidOperationException("Jwt configuration is missing.");
var signingKey = jwtOptions.SigningKey;
if (string.IsNullOrWhiteSpace(signingKey) && builder.Environment.IsDevelopment())
    signingKey = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
if (string.IsNullOrWhiteSpace(signingKey) || signingKey.Length < 32)
    throw new InvalidOperationException("Jwt:SigningKey must be configured with at least 32 characters using user-secrets or an environment variable.");
builder.Services.PostConfigure<JwtOptions>(options => options.SigningKey = signingKey);

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(signingKey)),
            ValidateIssuer = true,
            ValidIssuer = jwtOptions.Issuer,
            ValidateAudience = true,
            ValidAudience = jwtOptions.Audience,
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero
        };
    });
builder.Services.AddAuthorization();

builder.Services.AddScoped<IHealthProfileService, HealthProfileService>();
builder.Services.AddScoped<INotificationRepository, NotificationRepository>();
builder.Services.AddScoped<NotificationService>();
builder.Services.AddScoped<INotificationService>(sp => sp.GetRequiredService<NotificationService>());
builder.Services.AddScoped<INotificationPublisher>(sp => sp.GetRequiredService<NotificationService>());
builder.Services.AddScoped<NotificationPushDispatcher>();
builder.Services.AddScoped<NotificationPushJob>();
builder.Services.AddSingleton<IValidateOptions<WebPushSettings>, WebPushOptionsValidator>();
builder.Services.AddOptions<WebPushSettings>().BindConfiguration("WebPush").ValidateOnStart();
builder.Services.AddHttpClient<IWebPushTransport, WebPushTransport>(client => client.Timeout = TimeSpan.FromSeconds(15))
    .ConfigurePrimaryHttpMessageHandler(() => new HttpClientHandler { AllowAutoRedirect = false });
var pushWorkerConfigured = builder.Configuration.GetValue<bool>("WebPush:Enabled");
if (pushWorkerConfigured)
{
    var jobConnection = DatabaseConnection.Validate(builder.Configuration.GetConnectionString("HangfireConnection")
        ?? builder.Configuration.GetConnectionString("DefaultConnection"));
    builder.Services.AddHangfire(config => config.UseSimpleAssemblyNameTypeSerializer().UseRecommendedSerializerSettings()
        .UsePostgreSqlStorage(storage => storage.UseNpgsqlConnection(jobConnection),
            new PostgreSqlStorageOptions { PrepareSchemaIfNecessary = args.Contains("--prepare-push-jobs") }));
    builder.Services.AddHangfireServer(options => options.WorkerCount = 1);
}
builder.Services.AddScoped<IHealthProfileRepository, HealthProfileRepository>();
builder.Services.AddScoped<IShopRepository, ShopRepository>();
builder.Services.AddScoped<IShopService, ShopService>();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddOptions<MappingOptions>().BindConfiguration("AutoMapper");
builder.Services.AddAutoMapper((services, cfg) =>
    cfg.LicenseKey = services.GetRequiredService<IOptions<MappingOptions>>().Value.LicenseKey, typeof(MappingProfile));
builder.Services.AddScoped<CloudflareR2StorageService>();
builder.Services.AddScoped<IMediaStorageService>(sp => sp.GetRequiredService<CloudflareR2StorageService>());
builder.Services.AddScoped<IAvatarStorage>(sp => sp.GetRequiredService<CloudflareR2StorageService>());

var app = builder.Build();

if (args.Contains("--prepare-push-jobs"))
{
    if (!app.Services.GetRequiredService<IOptions<WebPushSettings>>().Value.Enabled)
        throw new InvalidOperationException("Configure WebPush and enable it before preparing Hangfire storage.");
    _ = app.Services.GetRequiredService<JobStorage>();
    // Hangfire uses a private schema; never expose its serialized job data through Data API roles.
    await using var jobDatabase = new Npgsql.NpgsqlConnection(DatabaseConnection.Validate(
        builder.Configuration.GetConnectionString("HangfireConnection") ?? builder.Configuration.GetConnectionString("DefaultConnection")));
    await jobDatabase.OpenAsync();
    await using var revoke = new Npgsql.NpgsqlCommand("""
        REVOKE ALL ON SCHEMA hangfire FROM PUBLIC;
        DO $$ DECLARE role_name text;
        BEGIN
            FOREACH role_name IN ARRAY ARRAY['anon', 'authenticated'] LOOP
                IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = role_name) THEN
                    EXECUTE format('REVOKE ALL ON SCHEMA hangfire FROM %I', role_name);
                END IF;
            END LOOP;
        END $$;
        """, jobDatabase);
    await revoke.ExecuteNonQueryAsync();
    Console.WriteLine("Hangfire PostgreSQL storage prepared.");
    return;
}

if (args.Contains("--migrate") || args.Contains("--seed") || args.Contains("--seed-posts"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
    if (args.Contains("--migrate"))
        await dbContext.Database.MigrateAsync();
    if (args.Contains("--seed") || args.Contains("--seed-posts"))
    {
        await using var transaction = await dbContext.Database.BeginTransactionAsync();
        await dbContext.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(3912026)");
        await DataSeeder.SeedDataAsync(dbContext);
        if (args.Contains("--seed-posts")) await PostSeeder.SeedPostsAsync(dbContext);
        await transaction.CommitAsync();
    }
    Console.WriteLine("Requested database operation completed.");
    return;
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseCors("Frontend");
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
if (pushWorkerConfigured &&
    !args.Any(a => a is "--migrate" or "--seed" or "--seed-posts"))
{
    app.Services.GetRequiredService<IRecurringJobManager>().AddOrUpdate<NotificationPushJob>(
        "notifications-web-push", job => job.RunAsync(CancellationToken.None), Cron.Minutely());
}
app.Run();

static string? FindEnvironmentFile(string startPath)
{
    var directory = new DirectoryInfo(startPath);

    while (directory is not null)
    {
        var candidate = Path.Combine(directory.FullName, ".env");
        if (File.Exists(candidate))
        {
            return candidate;
        }

        directory = directory.Parent;
    }

    return null;
}

public partial class Program { }
