using AutoMapper;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.MealPlanning;
using VeganHelper.BLL.Mapping;
using VeganHelper.BLL.Services.MealPlanning;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.UnitTests;

public sealed class MealRoutineEnrichmentTests
{
    [Fact]
    public async Task EnrichManyAsync_LoadsImagesRoutinesAndEntriesInBatches()
    {
        const long userId = 44;
        var planIds = new long[] { 1, 2 };
        using var cancellation = new CancellationTokenSource();
        var ct = cancellation.Token;
        var mealPlanning = new Mock<IMealPlanningRepository>();
        var mealRoutines = new Mock<IMealRoutineRepository>();
        var mapper = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance).CreateMapper();
        var routine = new MealPlanRoutine { MealPlanId = 1, UserId = userId, Mode = "weight_goal" };
        var entry = new MealPlanEntry { MealPlanId = 1, Date = new(2026, 10, 24), MealTime = "breakfast" };

        mealPlanning.Setup(repository => repository.GetImageUrlsAsync(
                It.Is<IReadOnlyCollection<long>>(ids => ids.Order().SequenceEqual(new long[] { 10, 20 })), ct))
            .ReturnsAsync(new Dictionary<long, string>
            {
                [10] = "https://media.example/10.jpg",
                [20] = "https://media.example/20.jpg"
            });
        mealRoutines.Setup(repository => repository.GetManyAsync(userId,
                It.Is<IReadOnlyCollection<long>>(ids => ids.Order().SequenceEqual(planIds)), ct))
            .ReturnsAsync([routine]);
        mealRoutines.Setup(repository => repository.EntriesAsync(
                It.Is<IReadOnlyCollection<long>>(ids => ids.SequenceEqual(new long[] { 1 })), ct))
            .ReturnsAsync([entry]);

        var service = new MealRoutineService(mealRoutines.Object, mealPlanning.Object,
            Mock.Of<IMealPlanAccessPolicy>(), mapper, TimeProvider.System, Mock.Of<IMealPlanPaymentService>());
        var plans = new[]
        {
            Plan(1, 10),
            Plan(2, 20)
        };

        await service.EnrichManyAsync(userId, plans, ct);

        Assert.Equal("https://media.example/10.jpg", Assert.Single(plans[0].Menu!.Days[0].Meals).ImageUrl);
        Assert.Equal("https://media.example/20.jpg", Assert.Single(plans[1].Menu!.Days[0].Meals).ImageUrl);
        Assert.NotNull(plans[0].Routine);
        Assert.Single(plans[0].Routine!.Entries);
        Assert.Null(plans[1].Routine);
        mealPlanning.Verify(repository => repository.GetImageUrlsAsync(It.IsAny<IReadOnlyCollection<long>>(), ct), Times.Once);
        mealRoutines.Verify(repository => repository.GetManyAsync(userId, It.IsAny<IReadOnlyCollection<long>>(), ct), Times.Once);
        mealRoutines.Verify(repository => repository.EntriesAsync(It.IsAny<IReadOnlyCollection<long>>(), ct), Times.Once);
    }

    private static MealPlanDto Plan(long planId, long mealId) => new()
    {
        Id = planId,
        Menu = new WeeklyMenuDto
        {
            Days = [new MealDayDto { Meals = [new ScheduledMealDto { MealId = mealId }] }]
        }
    };
}
