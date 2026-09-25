using System.Text;
using System.Security.Cryptography;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.DependencyInjection;
using VeganHelper.DAL.Storage;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllers();
builder.Services.AddProblemDetails();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();
builder.Services.AddDal(builder.Configuration);
builder.Services.AddScoped<IStatusService, StatusService>();
builder.Services.AddScoped<IAuthService, AuthService>();
builder.Services.AddScoped<IUserService, UserService>();
builder.Services.AddScoped<IJwtTokenService, JwtTokenService>();
builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

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
var app = builder.Build();
Directory.CreateDirectory(avatarRoot);
app.UseExceptionHandler();
if (app.Environment.IsDevelopment()) { app.UseSwagger(); app.UseSwaggerUI(); }
app.UseHttpsRedirection();
app.UseStaticFiles();
app.UseAuthentication();
app.UseAuthorization();
app.MapGet("/health/live", () => Results.Ok(new { status = "ok" }));
app.MapControllers();
app.Run();
