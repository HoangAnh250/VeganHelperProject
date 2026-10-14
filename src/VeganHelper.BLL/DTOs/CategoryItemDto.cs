namespace VeganHelper.BLL.DTOs;

public sealed class CategoryItemDto
{
    public int Id { get; init; }
    public string Name { get; init; } = string.Empty;
    public string Slug { get; init; } = string.Empty;
    public string? PostCategoryKind { get; init; }
}
