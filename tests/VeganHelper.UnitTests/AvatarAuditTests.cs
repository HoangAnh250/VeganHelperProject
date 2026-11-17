using Moq;
using SkiaSharp;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;
using VeganHelper.DAL.Storage;

namespace VeganHelper.UnitTests;

public sealed class AvatarAuditTests
{
    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task FailedAvatarChangeKeepsPreviousObject(bool saveDatabaseFails)
    {
        const string previous = "https://storage.example.invalid/avatars/previous.png";
        const string uploaded = "https://storage.example.invalid/avatars/uploaded.png";
        var error = new InvalidOperationException("Simulated avatar failure");
        var repository = new Mock<IUserRepository>();
        var storage = new Mock<IAvatarStorage>();
        repository.Setup(x => x.FindUserAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new User { Id = 1 });
        repository.Setup(x => x.FindUserProfileAsync(1, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new UserProfile { UserId = 1, AvatarUrl = previous });
        storage.Setup(x => x.OwnsAvatar(previous)).Returns(true);
        var save = storage.Setup(x => x.SaveAsync(It.IsAny<byte[]>(), "avatar.png", "image/png", It.IsAny<CancellationToken>()));
        if (saveDatabaseFails)
        {
            save.ReturnsAsync(uploaded);
            repository.Setup(x => x.SaveChangesAsync(It.IsAny<CancellationToken>())).ThrowsAsync(error);
        }
        else save.ThrowsAsync(error);

        using var bitmap = new SKBitmap(4, 4);
        using var image = SKImage.FromBitmap(bitmap);
        using var data = image.Encode(SKEncodedImageFormat.Png, 100);
        var request = new UpdateProfileCommand(null, null, null, null, null, null, null, data.ToArray(), "avatar.png", "image/png");

        Assert.Same(error, await Assert.ThrowsAsync<InvalidOperationException>(
            () => new UserService(repository.Object, storage.Object).UpdateProfileAsync(1, request, default)));
        storage.Verify(x => x.DeleteAvatarAsync(previous, It.IsAny<CancellationToken>()), Times.Never);
        storage.Verify(x => x.DeleteAvatarAsync(uploaded, It.IsAny<CancellationToken>()), saveDatabaseFails ? Times.Once() : Times.Never());
        if (!saveDatabaseFails)
            repository.Verify(x => x.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
