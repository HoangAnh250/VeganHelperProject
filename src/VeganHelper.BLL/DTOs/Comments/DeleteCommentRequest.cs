using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs.Comments;

public sealed class DeleteCommentRequest
{
    [StringLength(300, MinimumLength = 1)]
    public string? Reason { get; set; }
}
