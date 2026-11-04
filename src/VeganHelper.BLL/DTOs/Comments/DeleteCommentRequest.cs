using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs.Comments;

public sealed class DeleteCommentRequest
{
    public const int MaxReasonLength = 500;

    [StringLength(MaxReasonLength)]
    public string? Reason { get; set; }
}
