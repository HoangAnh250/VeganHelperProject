using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.IntegrationTests;

public partial class PostRepositoryTests
{
    [Theory]
    [InlineData("")]
    [InlineData("tofu")]
    public async Task SearchCategoryFiltersCountBeforePaginationAndExcludesPrivatePosts(string keyword)
    {
        var user = await AddUser();
        var category = new Category { Name = Guid.NewGuid().ToString("N"), Slug = Guid.NewGuid().ToString("N"), CategoryType = "post", PostCategoryKind = "recipe", IsActive = true };
        _context.Categories.Add(category);
        await _context.SaveChangesAsync();
        var posts = Enumerable.Range(0, 5).Select(i => {
            var p = NewPost(user.Id); p.Title = "tofu bowl";
            p.CreatedAt = DateTime.UtcNow.AddMinutes(i);
            p.PostCategories.Add(new PostCategory { CategoryId = category.Id });
            return p;
        }).ToArray();
        posts[3].Status = "pending_review"; posts[3].ModerationHold = true;
        posts[4].IsDeleted = true; posts[4].DeletedAt = DateTime.UtcNow;
        var otherCategory = NewPost(user.Id); otherCategory.Title = "tofu outside filter";
        _context.Posts.AddRange(posts); _context.Posts.Add(otherCategory);
        await _context.SaveChangesAsync();
        var first = await _postRepository.SearchPostsAsync(keyword, 1, 2, default, category.Id);
        var second = await _postRepository.SearchPostsAsync(keyword, 2, 2, default, category.Id);
        Assert.Equal(3, first.TotalCount); Assert.Equal(3, second.TotalCount);
        Assert.Equal(new[] { posts[2].Id, posts[1].Id }, first.Items.Select(p => p.Id));
        Assert.Equal(posts[0].Id, Assert.Single(second.Items).Id);
    }

    [Fact]
    public async Task DefaultPeopleBrowseOrdersByCreatedAtAndKeepsPrivateAccountsOut()
    {
        var older = new User { Username = "AAA_" + Guid.NewGuid().ToString("N"), Email = Guid.NewGuid().ToString("N") + "@example.com", RoleId = 1, IsActive = true, CreatedAt = DateTime.UtcNow.AddDays(2) };
        var newer = new User { Username = "ZZZ_" + Guid.NewGuid().ToString("N"), Email = Guid.NewGuid().ToString("N") + "@example.com", RoleId = 1, IsActive = true, CreatedAt = older.CreatedAt.AddHours(1) };
        _context.Users.AddRange(older, newer);
        await _context.SaveChangesAsync();
        var inactive = await AddUser(); inactive.CreatedAt = newer.CreatedAt.AddHours(1); inactive.IsActive = false;
        var deleted = await AddUser(); deleted.IsActive = false; deleted.DeletedAt = DateTime.UtcNow; deleted.CreatedAt = newer.CreatedAt.AddHours(2);
        var admin = await AddUser(); admin.CreatedAt = newer.CreatedAt.AddHours(3); admin.RoleId = await _context.Roles.Where(r => r.RoleName == "admin").Select(r => r.Id).SingleAsync();
        await _context.SaveChangesAsync();
        var repo = new UserRepository(_context);
        var first = await repo.SearchUsersAsync("", 1, 1, default);
        var second = await repo.SearchUsersAsync("", 2, 1, default);
        Assert.Equal(newer.Id, Assert.Single(first.Items).Id);
        Assert.Equal(older.Id, Assert.Single(second.Items).Id);
        Assert.DoesNotContain(first.Items.Concat(second.Items), p => p.Id == inactive.Id || p.Id == deleted.Id || p.Id == admin.Id);
    }
}
