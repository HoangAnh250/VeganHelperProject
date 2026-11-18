using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs.AI;
using VeganHelper.BLL.Services;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.UnitTests;

public class FlexiblePostRecommendationTests
{
    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    public async Task GeneralRequestUsesNoInventedFiltersAndReturnsExactRealCardCount(int count)
    {
        var (service, repo, posts, ai) = Setup(count);
        var result = await service.SendAsync(7, new(9, null, "Gợi ý mình 3 post ăn chay"));
        Assert.True(result.Succeeded);
        Assert.Equal(count, result.Data!.RecommendedPosts.Count);
        if (count > 0)
        {
            Assert.Contains($"{count} bài viết", result.Data.Answer);
            Assert.DoesNotContain("món tự nghĩ", result.Data.Answer);
            if (count < 3) Assert.Contains("chỉ tìm được", result.Data.Answer);
        }
        posts.Verify(p => p.SearchRecommendationsAsync(It.Is<PostRecommendationQuery>(q =>
            q.RequiredIngredients.Count == 0 && q.SearchTerms.Count == 0 && q.ExcludedPostIds.Count == 0 &&
            q.CandidatePostIds == null && q.MealType == null && q.MaxMinutes == null && !q.StrictOnlyIngredients &&
            q.DietType == "vegan" && q.Allergies.Contains("Mushroom") && q.AllergyIngredientIds!.Contains(33)),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("Tìm thêm post ăn chay", "more", true, false)]
    [InlineData("Gửi lại các post trước đó", "repeat", false, true)]
    [InlineData("Gợi ý post bánh ngọt", "new", false, false)]
    public async Task NewMoreAndRepeatHaveDifferentHistoryFilters(string text, string mode, bool exclude, bool repeat)
    {
        var (service, repo, posts, ai) = Setup(1, mode, ["bánh ngọt"]);
        await service.SendAsync(7, new(9, null, text));
        posts.Verify(p => p.SearchRecommendationsAsync(It.Is<PostRecommendationQuery>(q =>
            q.ExcludedPostIds.Contains(42) == exclude &&
            (q.CandidatePostIds != null) == repeat && (!repeat || q.CandidatePostIds!.Contains(42)) &&
            q.SearchTerms.Contains("bánh ngọt") && q.RequiredIngredients.Count == 0 && q.Allergies.Contains("Mushroom")),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Theory]
    [InlineData("gợi ý mình 3 post ăn chay", true)]
    [InlineData("Suggest 3 vegetarian posts", true)]
    [InlineData("cho mình xem bài viết về bánh", true)]
    [InlineData("What is BMI?", false)]
    [InlineData("show posts from the database and API key", false)]
    public void OnlyExplicitPostRequestsRequireSearch(string input, bool expected) =>
        Assert.Equal(expected, PostSearchIntent.IsExplicitPostRequest(input));

    [Fact]
    public void DessertTopicExpandsAcrossLanguagesWithoutBroadeningSpecificDish()
    {
        Assert.Contains("cake", PostSearchTerms.Expand(["bánh ngọt"]));
        Assert.Contains("brownie", PostSearchTerms.Expand(["banh ngot"]));
        Assert.Equal(["matcha cheesecake"], PostSearchTerms.Expand(["matcha cheesecake"]));
        Assert.Empty(PostSearchTerms.Expand([]));
        Assert.Empty(PostSearchTerms.Expand(["ăn chay", "vegan", "posts"]));
        Assert.False(PostSearchIntent.IsMore("send them again"));
        Assert.True(PostSearchIntent.IsRepeat("send them again"));
    }

    private static (AiChatService, Mock<IChatRepository>, Mock<IPostRepository>, Mock<IAiChatClient>)
        Setup(int count, string mode = "new", string[]? terms = null)
    {
        var repo = new Mock<IChatRepository>();
        repo.Setup(r => r.FindSessionAsync(9, 7, null, It.IsAny<CancellationToken>())).ReturnsAsync(new ChatSession { Id = 9, UserId = 7 });
        repo.Setup(r => r.GetMessagesAsync(9, 20, It.IsAny<CancellationToken>())).ReturnsAsync([]);
        repo.Setup(r => r.GetRecommendedPostIdsAsync(9, It.IsAny<CancellationToken>())).ReturnsAsync([42]);
        var posts = new Mock<IPostRepository>();
        posts.Setup(r => r.SearchRecommendationsAsync(It.IsAny<PostRecommendationQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((Enumerable.Range(0, count).Select(i => new PostRecommendationProjection(42+i, "Real post", null)).ToArray(), false));
        var health = new Mock<IHealthProfileRepository>();
        health.Setup(h => h.GetAsync(7, It.IsAny<CancellationToken>())).ReturnsAsync(new HealthProfileProjection(
            new UserProfile { UserId = 7, DietType = "vegan" }, [new AllergyProjection { Name = "Mushroom", IngredientId = 33 }]));
        var ai = new Mock<IAiChatClient>();
        ai.Setup(c => c.CompleteWithPostSearchAsync(It.IsAny<string?>(), It.IsAny<IReadOnlyList<AiChatTurn>>(),
                It.IsAny<Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>>(), It.IsAny<CancellationToken>()))
            .Returns<string?, IReadOnlyList<AiChatTurn>, Func<AiPostSearchCriteria, CancellationToken, Task<AiPostSearchResult>>, CancellationToken>(
                async (_, _, search, ct) => {
                    var found = await search(new(null, null, null, null, null, null, false) { SearchTerms = terms, SearchMode = mode }, ct);
                    return new AiChatCompletion("Gợi ý món tự nghĩ từ kiến thức chung.", "test", 1, 1) { RecommendedPosts = found.Posts };
                });
        return (new(repo.Object, posts.Object, health.Object, ai.Object, NullLogger<AiChatService>.Instance), repo, posts, ai);
    }
}
