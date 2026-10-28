using Moq;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.BLL.Exceptions;
using VeganHelper.BLL.Services.MealPlanning;

namespace VeganHelper.UnitTests;

public sealed class MealPlanningServiceAccessTests
{
    [Fact]
    public async Task ListAsync_NewUnpaidUserWithUnusedTrials_ReturnsEmptyPage()
    {
        const long userId = 42;
        var payments = new Mock<IMealPlanPaymentService>();
        payments.Setup(service => service.GetAccessAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MealPlanAccessDto("none", true, false, null, null, 3, false));
        var service = CreateService(payments.Object);

        var result = await service.ListAsync(userId, 1, 10, CancellationToken.None);

        Assert.Empty(result.Items);
        Assert.Equal(0, result.TotalCount);
        Assert.Equal(0, result.TotalPages);
        Assert.Equal(1, result.PageIndex);
        Assert.Equal(10, result.PageSize);
    }

    [Fact]
    public async Task ListAsync_ExpiredAccessWithoutTrialHistory_StillRequiresPayment()
    {
        const long userId = 42;
        var payments = new Mock<IMealPlanPaymentService>();
        payments.Setup(service => service.GetAccessAsync(userId, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new MealPlanAccessDto(
                "expired", false, false, DateTime.UtcNow.AddDays(-8), DateTime.UtcNow.AddDays(-1), 0, false));
        var service = CreateService(payments.Object);

        var exception = await Assert.ThrowsAsync<MealPlanAccessRequiredException>(
            () => service.ListAsync(userId, 1, 10, CancellationToken.None));

        Assert.Equal("MEAL_PLAN_VIEW_ACCESS_ENDED", exception.ErrorCode);
    }

    private static MealPlanningService CreateService(IMealPlanPaymentService payments) =>
        new(null!, null!, null!, null!, null!, null!, payments);
}
