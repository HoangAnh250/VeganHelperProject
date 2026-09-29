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
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs;
using VeganHelper.DAL.DependencyInjection;
using VeganHelper.DAL.Storage;

var builder = WebApplication.CreateBuilder(args);

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

var configuredAvatarRoot = builder.Configuration["Storage:AvatarRoot"];
var avatarRoot = string.IsNullOrWhiteSpace(configuredAvatarRoot)
    ? Path.Combine(builder.Environment.WebRootPath ?? Path.Combine(builder.Environment.ContentRootPath, "wwwroot"), "uploads", "avatars")
    : Path.GetFullPath(Path.IsPathRooted(configuredAvatarRoot)
        ? configuredAvatarRoot
        : Path.Combine(builder.Environment.ContentRootPath, configuredAvatarRoot));
builder.Services.AddSingleton<IAvatarStorage>(_ => new LocalAvatarStorage(avatarRoot));

builder.Services.AddScoped<IHealthProfileService, HealthProfileService>();
builder.Services.AddScoped<IMediaStorageService, CloudflareR2StorageService>();
builder.Services.AddValidatorsFromAssemblyContaining<CreatePostRequestValidator>();

var app = builder.Build();
Directory.CreateDirectory(avatarRoot);
app.UseExceptionHandler();
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
