using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs.Comments;

public sealed class DeleteCommentRequest
{
    public const int MaxReasonLength = 500;

    [StringLength(MaxReasonLength, MinimumLength = 1)]
    public string? Reason { get; set; }
}
