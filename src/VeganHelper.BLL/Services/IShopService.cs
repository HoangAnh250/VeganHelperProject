using VeganHelper.BLL.DTOs.Shops;

namespace VeganHelper.BLL.Services;

public interface IShopService
{
    Task<SearchShopsResponse> SearchShopsAsync(string keyword, decimal? latitude, decimal? longitude, int pageIndex, int pageSize);
}
