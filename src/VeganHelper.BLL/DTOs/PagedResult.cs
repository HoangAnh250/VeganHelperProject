using System.Collections.Generic;

namespace VeganHelper.BLL.DTOs;

public class PagedResult<T>
{
    public long TotalItems { get; set; }
    public int TotalCount { get; set; }
    public int PageIndex { get; set; }
    public int PageNumber => PageIndex;
    public int PageSize { get; set; }
    public int TotalPages { get; set; }
    public IEnumerable<T> Items { get; set; } = new List<T>();
}
