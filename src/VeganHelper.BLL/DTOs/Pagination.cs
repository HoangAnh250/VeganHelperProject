using AutoMapper;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.DTOs;

public static class Pagination
{
    public static void Validate(int pageIndex, int pageSize)
    {
        if (pageIndex < 1 || pageIndex > 1_000_000 || pageSize is < 1 or > 100) throw new ArgumentException("PageIndex must be 1–1000000 and PageSize must be 1–100.");
    }

    public static int TotalPages(long count, int pageSize) => checked((int)Math.Ceiling(count / (decimal)pageSize));

    public static PagedResult<TDto> Map<TEntity, TDto>(DatabasePage<TEntity> page, int pageIndex, int pageSize, IMapper mapper) => new()
    {
        Items = mapper.Map<List<TDto>>(page.Items), TotalItems = page.TotalCount, TotalCount = checked((int)page.TotalCount),
        PageIndex = pageIndex, PageSize = pageSize, TotalPages = TotalPages(page.TotalCount, pageSize)
    };
}
