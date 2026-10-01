namespace VeganHelper.BLL.DTOs.Posts;

public sealed record ToggleLikeResponse(
    long PostId,
    bool IsLiked,
    long LikeCount);

public sealed record ToggleSaveResponse(
    long PostId,
    bool IsSaved);
