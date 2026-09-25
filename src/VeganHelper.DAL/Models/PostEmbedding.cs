namespace VeganHelper.DAL.Models;

public sealed class PostEmbedding
{
    public long Id { get; set; }
    public long PostId { get; set; }
    public int ChunkIndex { get; set; }
    public string? VectorId { get; set; }
    public string ContentHash { get; set; } = string.Empty;
    public string EmbeddingModel { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public DateTime? EmbeddedAt { get; set; }
}
