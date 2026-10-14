using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Shops;

namespace VeganHelper.BLL.Services;

public interface IShopService
{
    Task<PagedResult<ShopListDto>> GetNearbyAsync(NearbyShopsRequest request, CancellationToken ct);
    Task<PagedResult<ShopListDto>> SearchAsync(SearchShopsRequest request, CancellationToken ct);
    Task<ShopDetailDto> GetDetailAsync(long id, ShopLocationRequest request, CancellationToken ct);
}
