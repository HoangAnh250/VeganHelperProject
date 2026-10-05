using AutoMapper;
using VeganHelper.BLL.DTOs.HealthProfile;
using VeganHelper.BLL.DTOs.Shops;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Mapping;

public sealed class MappingProfile : Profile
{
    public MappingProfile()
    {
        CreateMap<Notification, VeganHelper.BLL.DTOs.Notifications.NotificationDto>();
        CreateMap<UpdateHealthProfileRequest, UserProfile>(MemberList.None);
        CreateMap<UserProfile, HealthProfileDto>(MemberList.None);
        CreateMap<AllergyProjection, AllergyDto>();
        CreateMap<Ingredient, IngredientOptionDto>();
        CreateMap<BmiHistory, BmiHistoryDto>()
            .ForMember(d => d.Timestamp, o => o.MapFrom(s => s.RecordedAt))
            .ForMember(d => d.Bmi, o => o.MapFrom(s => s.BmiValue));
        CreateMap<ShopOpeningPeriod, ShopOpeningPeriodDto>();
        CreateMap<Shop, ShopListDto>()
            .ForMember(d => d.ShopId, o => o.MapFrom(s => s.Id))
            .ForMember(d => d.DistanceKm, o => o.Ignore())
            .ForMember(d => d.IsOpenNow, o => o.Ignore());
        CreateMap<ShopProjection, ShopListDto>().IncludeMembers(s => s.Shop)
            .ForMember(d => d.DistanceKm, o => o.MapFrom(s => s.DistanceKm))
            .ForMember(d => d.IsOpenNow, o => o.Ignore());
        CreateMap<Shop, ShopDetailDto>().IncludeBase<Shop, ShopListDto>()
            .ForMember(d => d.GoogleMapsDirectionsUrl, o => o.Ignore())
            .ForMember(d => d.MediaUrls, o => o.MapFrom(s => s.Media.OrderBy(m => m.DisplayOrder).ThenBy(m => m.Id).Select(m => m.MediaUrl)))
            .ForMember(d => d.MenuItems, o => o.MapFrom(s => s.MenuItems.OrderBy(m => m.Name).ThenBy(m => m.Id).Select(m => m.Name)));
    }
}
