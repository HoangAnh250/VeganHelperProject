namespace VeganHelper.DAL.Entities;

public sealed class Category
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Slug { get; set; } = string.Empty;
    public string CategoryType { get; set; } = string.Empty;
    public string? PostCategoryKind { get; set; }
    public bool IsActive { get; set; } = true;
}
