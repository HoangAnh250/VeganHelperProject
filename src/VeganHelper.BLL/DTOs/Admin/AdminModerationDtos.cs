using System.ComponentModel.DataAnnotations;
using VeganHelper.BLL.DTOs.Posts;
using VeganHelper.DAL.Integrations.Moderation;

namespace VeganHelper.BLL.DTOs.Admin;

public sealed class ModerationPostListRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    [MaxLength(100)] public string? Keyword { get; set; }
    public string Status { get; set; } = "pending_review";
}
public sealed class ModerationFlagListRequest
{
    public int PageIndex { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public long? PostId { get; set; }
    public double? MinConfidence { get; set; }
}
public sealed class ModerationDecisionRequest
{
    [Required] public string Action { get; set; } = "";
    [Required, MaxLength(1000)] public string Reason { get; set; } = "";
    [Range(1, int.MaxValue)] public int ExpectedRevision { get; set; }
}
public sealed class ModerationSettingsRequest
{
    public bool AutoPublishEnabled { get; set; }
    [Range(1, int.MaxValue)] public int ExpectedVersion { get; set; }
}
public sealed class ModerationSettingsDto
{
    public bool AutoPublishEnabled { get; set; }
    public int Version { get; set; }
    public DateTime? UpdatedAt { get; set; }
}
public class ModerationPostDto
{
    public long Id { get; set; }
    public long AuthorId { get; set; }
    public string Title { get; set; } = "";
    public string PostType { get; set; } = "";
    public string Status { get; set; } = "";
    public int ContentRevision { get; set; }
    public DateTime CreatedAt { get; set; }
    public string? ScanState { get; set; }
    public double? ConfidenceScore { get; set; }
}
public sealed class ModerationPostDetailDto : ModerationPostDto
{
    public string? Content { get; set; }
    public List<PostMediaDto> Media { get; set; } = [];
    public List<PostIngredientDto> Ingredients { get; set; } = [];
    public List<PostStepDto> Steps { get; set; } = [];
    public List<ModerationFlagDto> Flags { get; set; } = [];
    public List<ModerationDecisionDto> Decisions { get; set; } = [];
    public string? ScanErrorCode { get; set; }
    public string? ScanSummary { get; set; }
    public List<ModerationAiFinding> Findings { get; set; } = [];
}
public sealed class ModerationFlagDto
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public string Title { get; set; } = "";
    public int ContentRevision { get; set; }
    public string Reason { get; set; } = "";
    public string? AiModelName { get; set; }
    public double? ConfidenceScore { get; set; }
    public List<ModerationAiFinding> Findings { get; set; } = [];
    public DateTime CreatedAt { get; set; }
}
public sealed class ModerationDecisionDto
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public int Revision { get; set; }
    public string Action { get; set; } = "";
    public string ActorType { get; set; } = "";
    public long? AdminId { get; set; }
    public string Reason { get; set; } = "";
    public DateTime CreatedAt { get; set; }
}
