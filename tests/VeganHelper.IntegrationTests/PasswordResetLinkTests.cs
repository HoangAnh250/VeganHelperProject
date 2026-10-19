using System.Net;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class PasswordResetLinkTests(Member2ApiFixture fixture)
{
    [Theory]
    [InlineData("https://vegan-helper.example.test", "https://vegan-helper.example.test/forgot-password")]
    [InlineData("https://vegan-helper.example.test/", "https://vegan-helper.example.test/forgot-password")]
    [InlineData(" https://vegan-helper.example.test/ ", "https://vegan-helper.example.test/forgot-password")]
    [InlineData("http://localhost:3000", "http://localhost:3000/forgot-password")]
    public async Task ForgotPassword_EmailUsesConfiguredFrontendAndResetTokenStillWorks(string baseUrl, string expectedPage)
    {
        var sender = new RecordingEmailSender();
        using var factory = Factory(baseUrl, sender);
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { BaseAddress = new("https://localhost") });
        await using var db = fixture.Context();
        var suffix = Guid.NewGuid().ToString("N");
        var email = "reset+" + suffix + "@example.invalid";
        var user = new User { Username = "reset_" + suffix, Email = email, RoleId = 1, IsActive = true, PasswordHash = "old-hash", CreatedAt = DateTime.UtcNow };
        db.Users.Add(user); await db.SaveChangesAsync();
        var response = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var message = Assert.Single(sender.Messages);
        var link = message.PlainTextBody.Split('\n').Single(line => line.StartsWith("http", StringComparison.Ordinal));
        var uri = new Uri(link);
        Assert.Equal(expectedPage, uri.GetLeftPart(UriPartial.Path));
        Assert.Contains(WebUtility.HtmlEncode(link), message.HtmlBody);
        var query = uri.Query.TrimStart('?').Split('&').Select(pair => pair.Split('=', 2))
            .ToDictionary(pair => pair[0], pair => WebUtility.UrlDecode(pair[1]));
        Assert.Equal(email, query["email"]);
        var token = await db.PasswordResetTokens.AsNoTracking().SingleAsync(t => t.UserId == user.Id);
        Assert.NotEqual(query["token"], token.TokenHash);
        Assert.Equal(TimeSpan.FromMinutes(15), token.ExpiresAt - token.CreatedAt);
        const string password = "NewStrongPassword@123";
        var reset = new { email, token = query["token"], newPassword = password, confirmPassword = password };
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/reset-password", reset)).StatusCode);
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password", reset)).StatusCode);
        await db.Entry(user).ReloadAsync();
        Assert.True(BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
        var unknown = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email = "missing-" + suffix + "@example.invalid" });
        Assert.Equal(await response.Content.ReadAsStringAsync(), await unknown.Content.ReadAsStringAsync());
        Assert.Single(sender.Messages);
        Assert.Equal(HttpStatusCode.OK, (await client.PostAsJsonAsync("/api/auth/forgot-password", new { email })).StatusCode);
        var expiredLink = sender.Messages[1].PlainTextBody.Split('\n').Single(line => line.StartsWith("http", StringComparison.Ordinal));
        var expiredToken = WebUtility.UrlDecode(new Uri(expiredLink).Query.TrimStart('?').Split('&').Single(pair => pair.StartsWith("token=", StringComparison.Ordinal)).Split('=', 2)[1]);
        await db.PasswordResetTokens.Where(t => t.UserId == user.Id && t.UsedAt == null)
            .ExecuteUpdateAsync(update => update.SetProperty(t => t.ExpiresAt, DateTime.UtcNow.AddMinutes(-1)));
        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/auth/reset-password", new { email, token = expiredToken, newPassword = "AnotherStrongPassword@123" })).StatusCode);
        await db.Entry(user).ReloadAsync();
        Assert.True(BCrypt.Net.BCrypt.Verify(password, user.PasswordHash));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-url")]
    [InlineData("ftp://example.test")]
    [InlineData("https://user:password@example.test")]
    [InlineData("https://example.test/path")]
    [InlineData("https://example.test?next=other")]
    [InlineData("https://example.test#fragment")]
    public void InvalidFrontendConfiguration_IsRejectedAtStartup(string baseUrl)
    {
        using var factory = Factory(baseUrl, new RecordingEmailSender());
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => factory.CreateClient());
    }

    [Theory]
    [InlineData("http://localhost:3000")]
    [InlineData("https://localhost:3000")]
    [InlineData("http://vegan-helper.example.test")]
    public void Production_RejectsLocalOrInsecureFrontendOrigin(string baseUrl)
    {
        using var configured = Factory(baseUrl, new RecordingEmailSender());
        using var factory = configured.WithWebHostBuilder(builder =>
        {
            builder.UseSetting("environment", "Production");
            builder.ConfigureServices(services => services.RemoveAll<Microsoft.Extensions.Hosting.IHostedService>());
        });
        Assert.Throws<Microsoft.Extensions.Options.OptionsValidationException>(() => factory.CreateClient());
    }
    private WebApplicationFactory<Program> Factory(string baseUrl, RecordingEmailSender sender) =>
        fixture.Factory.WithWebHostBuilder(builder =>
        {
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(
                new Dictionary<string, string?> {
                    ["Frontend:BaseUrl"] = baseUrl,
                    ["PayOS:ReturnUrl"] = "https://frontend.example.test/meal-plan-payment/success",
                    ["PayOS:CancelUrl"] = "https://frontend.example.test/meal-plan-payment/cancel"
                }));
            builder.ConfigureServices(services =>
            {
                services.RemoveAll<IEmailSender>();
                services.AddSingleton<IEmailSender>(sender);
            });
        });

    private sealed class RecordingEmailSender : IEmailSender
    {
        public List<EmailMessage> Messages { get; } = [];
        public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
        {
            Messages.Add(message);
            return Task.CompletedTask;
        }
    }
}
