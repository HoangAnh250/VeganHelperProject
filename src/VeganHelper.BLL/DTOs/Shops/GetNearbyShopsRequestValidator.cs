using FluentValidation;

namespace VeganHelper.BLL.DTOs.Shops;

public class GetNearbyShopsRequestValidator : AbstractValidator<GetNearbyShopsRequest>
{
    public GetNearbyShopsRequestValidator()
    {
        RuleFor(x => x.Lat).InclusiveBetween(-90, 90).WithMessage("Vĩ độ không hợp lệ.");
        RuleFor(x => x.Lng).InclusiveBetween(-180, 180).WithMessage("Kinh độ không hợp lệ.");
        RuleFor(x => x.RadiusKm).GreaterThan(0).WithMessage("Bán kính phải lớn hơn 0.");
    }
}
