using System.Net;
using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

[Collection("Member2 API")]
public sealed class OtpProtectionTests(Member2ApiFixture fixture)
{
    [Fact]
    public async Task SameCodeCanVerifyTwoAccountsIndependently()
    {
        var first = await SeedAsync("741369");
        var second = await SeedAsync("741369");
        using var client = fixture.Factory.Client();
        foreach (var email in new[] { first, second })
        {
            using var result = await client.PostAsJsonAsync("/api/auth/verify-email", new { email, otp = "741369" });
            Assert.Equal(HttpStatusCode.OK, result.StatusCode);
        }
    }

    [Fact]
    public async Task FiveConcurrentWrongAttemptsExhaustTheCode()
    {
        var email = await SeedAsync("741370");
        using var client = fixture.Factory.Client();
        var wrong = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ =>
            client.PostAsJsonAsync("/api/auth/verify-email", new { email, otp = "741371" })));
        foreach (var response in wrong)
        {
            using (response) Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        using var correct = await client.PostAsJsonAsync("/api/auth/verify-email", new { email, otp = "741370" });
        Assert.Equal(HttpStatusCode.BadRequest, correct.StatusCode);
    }

    [Fact]
    public async Task ResendDuringCooldownKeepsTheExistingCode()
    {
        var email = await SeedAsync("741372");
        using var client = fixture.Factory.Client();
        var requests = await Task.WhenAll(Enumerable.Range(0, 4).Select(_ =>
            client.PostAsJsonAsync("/api/auth/resend-verification", new { email })));
        foreach (var response in requests)
        {
            using (response) Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var correct = await client.PostAsJsonAsync("/api/auth/verify-email", new { email, otp = "741372" });
        Assert.Equal(HttpStatusCode.OK, correct.StatusCode);
        await using var db = fixture.Context();
        var userId = await db.Users.Where(u => u.Email == email).Select(u => u.Id).SingleAsync();
        Assert.Equal(1, await db.EmailVerificationTokens.CountAsync(t => t.UserId == userId));
    }

    [Fact]
    public async Task AValidCodeCanOnlyBeConsumedOnce()
    {
        var email = await SeedAsync("741373");
        using var client = fixture.Factory.Client();
        var confirmations = await Task.WhenAll(Enumerable.Range(0, 2).Select(_ =>
            client.PostAsJsonAsync("/api/auth/verify-email", new { email, otp = "741373" })));
        try
        {
            Assert.Single(confirmations, response => response.StatusCode == HttpStatusCode.OK);
            Assert.Single(confirmations, response => response.StatusCode == HttpStatusCode.BadRequest);
        }
        finally
        {
            foreach (var response in confirmations) response.Dispose();
        }
    }

    [Fact]
    public async Task AnOtpIssuedForAnotherPurposeCannotVerifyRegistration()
    {
        var email = await SeedAsync("741374", "google_unlink");
        using var client = fixture.Factory.Client();
        using var response = await client.PostAsJsonAsync("/api/auth/verify-email", new { email, otp = "741374" });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task PublicResendIsRateLimitedEvenForUnknownAccounts()
    {
        using var app = fixture.Factory.WithWebHostBuilder(_ => { });
        using var client = app.CreateClient(new() { BaseAddress = new Uri("https://localhost") });
        for (var i = 0; i < 12; i++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/resend-verification",
                new { email = $"missing-{i}@example.invalid" });
            Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        }
        using var limited = await client.PostAsJsonAsync("/api/auth/resend-verification",
            new { email = "missing-over-limit@example.invalid" });
        Assert.Equal(HttpStatusCode.TooManyRequests, limited.StatusCode);
        Assert.True(limited.Headers.Contains("Retry-After"));
    }

    private async Task<string> SeedAsync(string otp, string purpose = "registration")
    {
        await using var db = fixture.Context();
        var name = Guid.NewGuid().ToString("N");
        var user = new User { Username = name, Email = name + "@example.invalid", RoleId = 1, CreatedAt = DateTime.UtcNow };
        db.Users.Add(user);
        await db.SaveChangesAsync();
        db.EmailVerificationTokens.Add(new EmailVerificationToken
        {
            UserId = user.Id, TargetEmail = user.Email, Purpose = purpose,
            TokenHash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(otp))).ToLowerInvariant(),
            CreatedAt = DateTime.UtcNow, ExpiresAt = DateTime.UtcNow.AddMinutes(15)
        });
        await db.SaveChangesAsync();
        return user.Email;
    }
}
