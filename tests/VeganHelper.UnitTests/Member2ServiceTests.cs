using AutoMapper;
using FluentValidation;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VeganHelper.BLL.DTOs.HealthProfile;
using VeganHelper.BLL.DTOs.Shops;
using VeganHelper.BLL.Exceptions;
using VeganHelper.BLL.Mapping;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.UnitTests;

public sealed class Member2ServiceTests
{
    private static IMapper Mapper()
    {
        var configuration = new MapperConfiguration(c => c.AddProfile<MappingProfile>(), NullLoggerFactory.Instance);
        configuration.AssertConfigurationIsValid();
        return configuration.CreateMapper();
    }

    private static HealthProfileService Health(Mock<IHealthProfileRepository> repository) =>
        new(repository.Object, Mapper(), new UpdateHealthProfileRequestValidator(), new DeclareAllergiesRequestValidator());

    private static UpdateHealthProfileRequest Request() => new()
    {
        HeightCm = 175, WeightKg = 70, BirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-30),
        BiologicalSex = "male", DietType = "vegan", ActivityLevel = "sedentary"
    };

    [Theory]
    [InlineData(99.99, 70)]
    [InlineData(250.01, 70)]
    [InlineData(175, 29.99)]
    [InlineData(175, 200.01)]
    public async Task UpdateHealthProfileAsync_WhenMeasurementsAreOutsideBounds_RejectsWithoutWriting(decimal height, decimal weight)
    {
        var repository = new Mock<IHealthProfileRepository>();
        var request = Request();
        request.HeightCm = height;
        request.WeightKg = weight;

        await Assert.ThrowsAsync<ValidationException>(() => Health(repository).UpdateHealthProfileAsync(1, request));

        repository.Verify(r => r.UpdateAsync(It.IsAny<long>(), It.IsAny<UserProfile>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData(100, 30)]
    [InlineData(250, 200)]
    public async Task UpdateHealthProfileAsync_WhenMeasurementsAreOnBounds_PersistsProfile(decimal height, decimal weight)
    {
        var repository = new Mock<IHealthProfileRepository>();
        var request = Request();
        request.HeightCm = height;
        request.WeightKg = weight;

        await Health(repository).UpdateHealthProfileAsync(1, request);

        repository.Verify(r => r.UpdateAsync(1, It.Is<UserProfile>(p => p.HeightCm == height && p.WeightKg == weight), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateHealthProfileAsync_WhenInputsAreValid_CalculatesBmiAndAdultTdee()
    {
        var repository = new Mock<IHealthProfileRepository>();
        var request = Request();

        var result = await Health(repository).UpdateHealthProfileAsync(1, request);

        Assert.Equal(22.86m, result.CurrentBmi);
        Assert.Equal("Normal", result.BmiCategory);
        Assert.Equal(1978.50m, result.EstimatedTdee);
        repository.Verify(r => r.UpdateAsync(1, It.IsAny<UserProfile>(), 22.86m, It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task UpdateHealthProfileAsync_WhenBirthDateIsInFuture_RejectsWithoutWriting()
    {
        var repository = new Mock<IHealthProfileRepository>();
        var request = Request();
        request.BirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddDays(1);

        await Assert.ThrowsAsync<ValidationException>(() => Health(repository).UpdateHealthProfileAsync(1, request));

        repository.VerifyNoOtherCalls();
    }

    [Theory]
    [InlineData("other", 30)]
    [InlineData("male", 12)]
    public async Task UpdateHealthProfileAsync_WhenTdeeInputsAreUnsupported_DoesNotInventCalorieEstimate(string sex, int age)
    {
        var repository = new Mock<IHealthProfileRepository>();
        var request = Request();
        request.BiologicalSex = sex;
        request.BirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-age);

        var result = await Health(repository).UpdateHealthProfileAsync(1, request);

        Assert.Null(result.EstimatedTdee);
        if (age < 18) Assert.Null(result.BmiCategory);
        repository.Verify(r => r.UpdateAsync(1, It.IsAny<UserProfile>(), It.IsAny<decimal>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task GetBmiResultAsync_WhenLegacyBirthDateIsNull_ReturnsBmiWithoutServerError()
    {
        var repository = new Mock<IHealthProfileRepository>();
        repository.Setup(r => r.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new HealthProfileProjection(new UserProfile { HeightCm = 175, WeightKg = 70, BiologicalSex = "male", ActivityLevel = "sedentary" }, []));

        var result = await Health(repository).GetBmiResultAsync(1);

        Assert.Equal(22.86m, result.Bmi);
        Assert.Null(result.DailyCalorieRecommendation);
    }

    [Fact]
    public async Task GetBmiResultAsync_WhenUserIsMinor_DoesNotApplyAdultClassificationOrWeightTargets()
    {
        var repository = new Mock<IHealthProfileRepository>();
        repository.Setup(r => r.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new HealthProfileProjection(
            new UserProfile { HeightCm = 175, WeightKg = 70, BirthDate = DateOnly.FromDateTime(DateTime.UtcNow).AddYears(-12) }, []));

        var result = await Health(repository).GetBmiResultAsync(1);

        Assert.Equal(22.86m, result.Bmi);
        Assert.Null(result.Category);
        Assert.Null(result.IdealWeightRange);
        Assert.Null(result.DailyCalorieRecommendation);
    }

    [Fact]
    public async Task GetBmiResultAsync_WhenHeightIsMissing_ThrowsBadRequestInsteadOfDereferencingNull()
    {
        var repository = new Mock<IHealthProfileRepository>();
        repository.Setup(r => r.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new HealthProfileProjection(new UserProfile { WeightKg = 70 }, []));

        var exception = await Assert.ThrowsAsync<ArgumentException>(() => Health(repository).GetBmiResultAsync(1));

        Assert.Contains("height", exception.Message);
    }

    [Theory]
    [InlineData(18.49, "Underweight")]
    [InlineData(18.5, "Normal")]
    [InlineData(24.99, "Normal")]
    [InlineData(25, "Overweight")]
    [InlineData(29.99, "Overweight")]
    [InlineData(30, "Obese")]
    public async Task GetBmiResultAsync_WhenBmiCrossesCategoryBoundary_ReturnsExpectedCategory(decimal weight, string category)
    {
        var repository = new Mock<IHealthProfileRepository>();
        repository.Setup(r => r.GetAsync(1, It.IsAny<CancellationToken>())).ReturnsAsync(new HealthProfileProjection(new UserProfile { HeightCm = 200, WeightKg = weight * 4 }, []));

        var result = await Health(repository).GetBmiResultAsync(1);

        Assert.Equal(category, result.Category);
    }

    [Fact]
    public async Task DeclareAllergiesAsync_WhenInputsRepeat_TrimsAndDeduplicatesBeforeSaving()
    {
        var repository = new Mock<IHealthProfileRepository>();
        var request = new DeclareAllergiesRequest { AllergyIngredientIds = [1, 1], CustomAllergies = [" Đậu ", "đậu"] };

        await Health(repository).DeclareAllergiesAsync(7, request);

        repository.Verify(r => r.ReplaceAllergiesAsync(7, It.Is<IReadOnlyCollection<long>>(v => v.Count == 1), It.Is<IReadOnlyCollection<string>>(v => v.Count == 1 && v.Single() == "Đậu"), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" ")]
    public async Task DeclareAllergiesAsync_WhenCustomNameIsEmpty_RejectsWithoutWriting(string? name)
    {
        var repository = new Mock<IHealthProfileRepository>();
        var request = new DeclareAllergiesRequest { CustomAllergies = [name!] };

        await Assert.ThrowsAsync<ValidationException>(() => Health(repository).DeclareAllergiesAsync(7, request));

        repository.VerifyNoOtherCalls();
    }

    private sealed class TestClock(DateTimeOffset now) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => now;
    }

    private static ShopService Shops(Mock<IShopRepository> repository, DateTimeOffset? now = null) => new(repository.Object, Mapper(),
        new TestClock(now ?? DateTimeOffset.Parse("2026-10-05T17:30:00Z")), new NearbyShopsRequestValidator(), new SearchShopsRequestValidator(), new ShopLocationRequestValidator());

    [Fact]
    public async Task GetDetailAsync_WhenPeriodClosesAfterMidnight_ReturnsOpenInVietnamTimezone()
    {
        var repository = new Mock<IShopRepository>();
        repository.Setup(r => r.GetDetailAsync(1, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new ShopProjection
        {
            Shop = new Shop { Id = 1, Latitude = 10.75m, Longitude = 106.7m, OpeningPeriods = [new ShopOpeningPeriod { DayOfWeek = 1, OpensAt = new TimeOnly(22, 0), ClosesAt = new TimeOnly(2, 0) }] }
        });

        var result = await Shops(repository).GetDetailAsync(1, new(), default);

        Assert.True(result.IsOpenNow);
        Assert.Contains("destination=10.75%2C106.7", result.GoogleMapsDirectionsUrl);
    }

    [Fact]
    public async Task GetDetailAsync_WhenMetadataIsUnknown_PreservesNulls()
    {
        var repository = new Mock<IShopRepository>();
        repository.Setup(r => r.GetDetailAsync(1, null, null, It.IsAny<CancellationToken>())).ReturnsAsync(new ShopProjection { Shop = new Shop { Id = 1 } });

        var result = await Shops(repository).GetDetailAsync(1, new(), default);

        Assert.Null(result.IsOpenNow);
        Assert.Null(result.Rating);
        Assert.Null(result.GoogleMapsDirectionsUrl);
    }

    [Fact]
    public async Task GetDetailAsync_WhenShopIsUnavailable_ThrowsNotFound()
    {
        var repository = new Mock<IShopRepository>();

        var exception = await Assert.ThrowsAsync<NotFoundException>(() => Shops(repository).GetDetailAsync(1, new(), default));

        Assert.Equal("Shop not found.", exception.Message);
    }

    [Theory]
    [InlineData(null, 106.7, 5)]
    [InlineData(91d, 106.7, 5)]
    [InlineData(10.7, 181d, 5)]
    [InlineData(10.7, 106.7, 4.99)]
    [InlineData(10.7, 106.7, 10.01)]
    public async Task GetNearbyAsync_WhenLocationOrRadiusIsInvalid_RejectsBeforeQuerying(double? lat, double? lng, double radius)
    {
        var repository = new Mock<IShopRepository>();
        var request = new NearbyShopsRequest { Lat = lat, Lng = lng, RadiusKm = radius };

        await Assert.ThrowsAsync<ValidationException>(() => Shops(repository).GetNearbyAsync(request, default));

        repository.VerifyNoOtherCalls();
    }
}
