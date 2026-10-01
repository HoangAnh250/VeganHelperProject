using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class PostInteractionService(IPostInteractionRepository repository) : IPostInteractionService
{
    public async Task<ToggleLikeResponse> ToggleLikeAsync(
        long userId,
        long postId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePublishedPostAsync(postId, cancellationToken);

        var existingLike = await repository.FindLikeAsync(userId, postId, cancellationToken);
        bool isLiked;

        if (existingLike is null)
        {
            await repository.AddLikeAsync(new PostLike
            {
                UserId = userId,
                PostId = postId,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
            isLiked = true;
        }
        else
        {
            repository.RemoveLike(existingLike);
            isLiked = false;
        }

        await repository.SaveChangesAsync(cancellationToken);
        var likeCount = await repository.CountLikesAsync(postId, cancellationToken);

        return new ToggleLikeResponse(postId, isLiked, likeCount);
    }

    public async Task<ToggleSaveResponse> ToggleSaveAsync(
        long userId,
        long postId,
        CancellationToken cancellationToken = default)
    {
        await EnsurePublishedPostAsync(postId, cancellationToken);

        var existingSavedPost = await repository.FindSavedPostAsync(userId, postId, cancellationToken);
        bool isSaved;

        if (existingSavedPost is null)
        {
            await repository.AddSavedPostAsync(new SavedPost
            {
                UserId = userId,
                PostId = postId,
                SavedAt = DateTime.UtcNow
            }, cancellationToken);
            isSaved = true;
        }
        else
        {
            repository.RemoveSavedPost(existingSavedPost);
            isSaved = false;
        }

        await repository.SaveChangesAsync(cancellationToken);
        return new ToggleSaveResponse(postId, isSaved);
    }

    private async Task EnsurePublishedPostAsync(long postId, CancellationToken cancellationToken)
    {
        if (postId <= 0 || !await repository.IsPublishedPostAsync(postId, cancellationToken))
        {
            throw new VeganHelper.BLL.Exceptions.NotFoundException(
                $"Published post with ID {postId} was not found.");
        }
    }
}
