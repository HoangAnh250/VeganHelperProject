namespace VeganHelper.BLL.DTOs;

public class PagedResult<T>
{
    public long TotalItems { get; set; }
    public int TotalPages { get; set; }
    public IEnumerable<T> Items { get; set; } = new List<T>();
}
