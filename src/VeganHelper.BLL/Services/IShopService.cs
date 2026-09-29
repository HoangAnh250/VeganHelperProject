using VeganHelper.BLL.DTOs.Shops;

namespace VeganHelper.BLL.Services;

public interface IShopService
{
    Task<List<ShopNearbyDto>> GetNearbyShopsAsync(GetNearbyShopsRequest request);
}
