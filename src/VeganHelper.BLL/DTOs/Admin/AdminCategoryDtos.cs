using System.ComponentModel.DataAnnotations;

namespace VeganHelper.BLL.DTOs.Admin;

public sealed class AdminCategoryListRequest
{
    public int PageIndex { get; init; } = 1;
    public int PageSize { get; init; } = 20;
    [StringLength(100)] public string? Keyword { get; init; }
    public string? PostCategoryKind { get; init; }
    public bool? IsActive { get; init; }
}
public sealed class AdminCategoryWriteRequest
{
    [Required, StringLength(100)] public string Name { get; init; } = "";
    [Required, StringLength(120)] public string Slug { get; init; } = "";
    [Required, StringLength(20)] public string PostCategoryKind { get; init; } = "";
}
public sealed class AdminCategoryDto
{
    public int Id { get; init; }
    public string Name { get; init; } = "";
    public string Slug { get; init; } = "";
    public string CategoryType { get; init; } = "";
    public string? PostCategoryKind { get; init; }
    public bool IsActive { get; init; }
    public long PostCount { get; init; }
}
