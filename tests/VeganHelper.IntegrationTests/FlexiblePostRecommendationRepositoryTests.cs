using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.IntegrationTests;

public partial class PostRepositoryTests
{
    [Fact]
    public async Task DessertTopicUsesTitlesAcrossLanguagesAndNeverRelaxesAllergyOrVisibility()
    {
        var user = await AddUser();
        var safe = new Ingredient { Name = "Flour " + Guid.NewGuid().ToString("N"), DefaultUnit = "g" };
        var allergy = new Ingredient { Name = "Mushroom " + Guid.NewGuid().ToString("N"), DefaultUnit = "g" };
        _context.Ingredients.AddRange(safe, allergy);
        await _context.SaveChangesAsync();
        var valid = NewPost(user.Id); valid.Title = "Vegan Chocolate Cake";
        var blocked = NewPost(user.Id); blocked.Title = "Vegan mushroom cake";
        var missing = NewPost(user.Id); missing.Title = "Vegan Brownie with no linked ingredients";
        var hidden = NewPost(user.Id); hidden.Title = "Private cheesecake"; hidden.ModerationHold = true;
        var unrelated = NewPost(user.Id); unrelated.Title = "Tofu lunch";
        var deleted = NewPost(user.Id); deleted.Title = "Deleted banana cake"; deleted.IsDeleted = true; deleted.DeletedAt = DateTime.UtcNow;
        foreach (var post in new[] { valid, blocked, missing, hidden, unrelated, deleted })
        {
            post.DietType = "vegan"; post.IngredientsVerified = true;
            if (post != missing) post.PostIngredients.Add(new() { IngredientId = post == blocked ? allergy.Id : safe.Id, Quantity = 10, Unit = "g" });
            _context.Posts.Add(post);
        }
        await _context.SaveChangesAsync();
        var query = new PostRecommendationQuery([], [], ["mushroom"], [], null, "vegan", null, false, [allergy.Id]) { SearchTerms = ["bánh ngọt"] };
        var result = await _postRepository.SearchRecommendationsAsync(query);
        Assert.Equal(valid.Id, Assert.Single(result.Posts).PostId);
        Assert.Empty((await _postRepository.SearchRecommendationsAsync(query with { ExcludedPostIds = [valid.Id] })).Posts);
        Assert.Equal(valid.Id, Assert.Single((await _postRepository.SearchRecommendationsAsync(query with { CandidatePostIds = [valid.Id] })).Posts).PostId);
        Assert.Empty((await _postRepository.SearchRecommendationsAsync(query with { CandidatePostIds = [blocked.Id, missing.Id] })).Posts);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    public async Task BroadRequestReturnsUpToThreeWithoutRequiringMealOrIngredients(int count)
    {
        var user = await AddUser();
        var added = Enumerable.Range(0, count).Select(i => {
            var post = NewPost(user.Id); post.Title = "Vegan post " + i; post.DietType = "vegan"; return post;
        }).ToArray();
        _context.Posts.AddRange(added); await _context.SaveChangesAsync();
        var query = new PostRecommendationQuery([], [], [], [], null, "vegan", null, false) { CandidatePostIds = added.Select(p => p.Id).ToArray() };
        var found = await _postRepository.SearchRecommendationsAsync(query);
        Assert.Equal(Math.Min(3, count), found.Posts.Count);
        Assert.Equal(count > 3, found.HasMore);
    }
}
