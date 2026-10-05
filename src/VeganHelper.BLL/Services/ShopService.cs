using System.Globalization;
using AutoMapper;
using FluentValidation;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Shops;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed class ShopService(IShopRepository repository, IMapper mapper, TimeProvider clock,
    IValidator<NearbyShopsRequest> nearbyValidator, IValidator<SearchShopsRequest> searchValidator,
    IValidator<ShopLocationRequest> locationValidator) : IShopService
{
    public async Task<PagedResult<ShopListDto>> GetNearbyAsync(NearbyShopsRequest request, CancellationToken ct)
    {
        await nearbyValidator.ValidateAndThrowAsync(request, ct);
        var page = await repository.SearchAsync(null, request.Lat, request.Lng, request.RadiusKm, request.PageIndex, request.PageSize, ct);
        return MapPage(page, request.PageIndex, request.PageSize);
    }

    public async Task<PagedResult<ShopListDto>> SearchAsync(SearchShopsRequest request, CancellationToken ct)
    {
        request.Keyword = request.Keyword?.Trim() ?? "";
        await searchValidator.ValidateAndThrowAsync(request, ct);
        var page = await repository.SearchAsync(request.Keyword, request.Lat, request.Lng, null, request.PageIndex, request.PageSize, ct);
        return MapPage(page, request.PageIndex, request.PageSize);
    }

    public async Task<ShopDetailDto> GetDetailAsync(long id, ShopLocationRequest request, CancellationToken ct)
    {
        await locationValidator.ValidateAndThrowAsync(request, ct);
        var projection = await repository.GetDetailAsync(id, request.Lat, request.Lng, ct) ?? throw new NotFoundException("Shop not found.");
        var dto = mapper.Map<ShopDetailDto>(projection.Shop);
        dto.DistanceKm = RoundDistance(projection.DistanceKm);
        dto.IsOpenNow = IsOpen(projection.Shop.OpeningPeriods);
        if (dto.Latitude.HasValue && dto.Longitude.HasValue)
        {
            var destination = FormattableString.Invariant($"{dto.Latitude.Value},{dto.Longitude.Value}");
            dto.GoogleMapsDirectionsUrl = "https://www.google.com/maps/dir/?api=1&destination=" + Uri.EscapeDataString(destination);
            if (request.Lat.HasValue) dto.GoogleMapsDirectionsUrl += "&origin=" + Uri.EscapeDataString(FormattableString.Invariant($"{request.Lat.Value},{request.Lng!.Value}"));
        }
        return dto;
    }

    private PagedResult<ShopListDto> MapPage(DatabasePage<ShopProjection> page, int pageIndex, int pageSize)
    {
        var dto = Pagination.Map<ShopProjection, ShopListDto>(page, pageIndex, pageSize, mapper);
        var items = dto.Items.ToList();
        for (var index = 0; index < items.Count; index++)
        {
            items[index].IsOpenNow = IsOpen(page.Items[index].Shop.OpeningPeriods);
            items[index].DistanceKm = RoundDistance(page.Items[index].DistanceKm);
        }
        dto.Items = items;
        return dto;
    }

    private static double? RoundDistance(double? distance) => distance.HasValue ? Math.Round(distance.Value, 3) : null;

    private bool? IsOpen(ICollection<ShopOpeningPeriod> periods)
    {
        if (periods.Count == 0) return null;
        var local = clock.GetUtcNow().ToOffset(TimeSpan.FromHours(7));
        var weekday = local.DayOfWeek == DayOfWeek.Sunday ? 7 : (int)local.DayOfWeek;
        var yesterday = weekday == 1 ? 7 : weekday - 1;
        var time = TimeOnly.FromDateTime(local.DateTime);
        return periods.Any(p => !p.IsClosed && (
            p.OpensAt < p.ClosesAt
                ? p.DayOfWeek == weekday && time >= p.OpensAt && time < p.ClosesAt
                : p.DayOfWeek == weekday && time >= p.OpensAt || p.DayOfWeek == yesterday && time < p.ClosesAt));
    }
}
