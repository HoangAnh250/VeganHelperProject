using FluentValidation;

namespace VeganHelper.BLL.DTOs.Shops;

public class ShopLocationRequestValidator : AbstractValidator<ShopLocationRequest>
{
    public ShopLocationRequestValidator()
    {
        RuleFor(x => x.Lat).Must(lat => !lat.HasValue || double.IsFinite(lat.Value) && lat.Value is >= -90 and <= 90);
        RuleFor(x => x.Lng).Must(lng => !lng.HasValue || double.IsFinite(lng.Value) && lng.Value is >= -180 and <= 180);
        RuleFor(x => x).Must(x => x.Lat.HasValue == x.Lng.HasValue).WithMessage("Lat and Lng must be supplied together.");
    }
}

public sealed class NearbyShopsRequestValidator : AbstractValidator<NearbyShopsRequest>
{
    public NearbyShopsRequestValidator()
    {
        Include(new ShopLocationRequestValidator());
        RuleFor(x => x.Lat).NotNull();
        RuleFor(x => x.Lng).NotNull();
        RuleFor(x => x.RadiusKm).Must(v => double.IsFinite(v) && v is >= 5 and <= 10).WithMessage("RadiusKm must be 5–10 km.");
        RuleFor(x => x.PageIndex).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}

public sealed class SearchShopsRequestValidator : AbstractValidator<SearchShopsRequest>
{
    public SearchShopsRequestValidator()
    {
        Include(new ShopLocationRequestValidator());
        RuleFor(x => x.Keyword).NotEmpty().Length(2, 100);
        RuleFor(x => x.PageIndex).InclusiveBetween(1, 1_000_000);
        RuleFor(x => x.PageSize).InclusiveBetween(1, 100);
    }
}
