namespace VeganHelper.BLL.Services;

public interface ICommentKeywordFilter
{
    CommentModerationResult Evaluate(string content);
}

public enum CommentModerationAction
{
    Allow,
    HidePendingReview,
    BlockRejected
}

public sealed record CommentModerationResult(
    CommentModerationAction Action,
    string? MatchedKeyword = null);
