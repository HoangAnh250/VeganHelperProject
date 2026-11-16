using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using VeganHelper.BLL.Services;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

public class OptionalReliabilityTests
{
    [Fact]
    public async Task ResetPasswordRevokesExistingAccessTokens()
    {
        var repo = new Mock<IAuthRepository>();
        var user = new User { Id = 123, Email = "review@example.invalid", TokenVersion = 7, IsActive = true };
        repo.Setup(r => r.FindUserByEmailAsync(user.Email, It.IsAny<CancellationToken>())).ReturnsAsync(user);
        repo.Setup(r => r.FindPasswordResetTokenAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new PasswordResetToken { UserId = user.Id, ExpiresAt = DateTime.UtcNow.AddMinutes(5) });
        var auth = new AuthService(repo.Object, Mock.Of<IUserRepository>(), Mock.Of<IEmailSender>(),
            Mock.Of<IGoogleTokenValidator>(), Mock.Of<IJwtTokenService>(), Options.Create(new JwtOptions()),
            NullLogger<AuthService>.Instance, Options.Create(new FrontendOptions()));
        var result = await auth.ResetPasswordAsync(new ResetPasswordRequestDto { Email = user.Email, Token = "token", NewPassword = "NewPassword789!" }, default);
        Assert.True(result.Succeeded);
        Assert.Equal(8, user.TokenVersion);
    }
    [Fact]
    public async Task ExpiredProcessingSummaryCanBeRetriedByTheUser()
    {
        var repo = new Mock<IPostSummaryRepository>();
        repo.Setup(r => r.GetPublicSourceAsync(50, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new SummarySource(50, 2, [new(10, "clip", 0)]));
        repo.Setup(r => r.GetAsync(50, It.IsAny<CancellationToken>())).ReturnsAsync(new PostSummary
            { PostId = 50, ContentRevision = 2, SourceMediaIdsJson = "[10]", Status = "processing", RequestedAt = DateTime.UtcNow.AddHours(-1) });
        var service = new PostSummaryService(repo.Object, Mock.Of<IVideoSummaryAiClient>(), Mock.Of<IPostSummaryQueue>(), NullLogger<PostSummaryService>.Instance);
        Assert.Equal("failed", (await service.GetAsync(50, default))!.Status);
        await service.RequestAsync(50, default);
        repo.Verify(r => r.RecoverExpiredAsync(50, It.IsAny<DateTime>(), It.IsAny<CancellationToken>()), Times.Once);
    }
}
