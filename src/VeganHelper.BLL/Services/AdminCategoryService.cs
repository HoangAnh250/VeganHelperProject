using System.Globalization;
using System.Text.RegularExpressions;
using AutoMapper;
using VeganHelper.BLL.Contracts;
using VeganHelper.BLL.DTOs;
using VeganHelper.BLL.DTOs.Admin;
using VeganHelper.BLL.Exceptions;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Repositories;

namespace VeganHelper.BLL.Services;

public sealed partial class AdminCategoryService(IAdminCategoryRepository repository, IAdminAuditService audit, IMapper mapper) : IAdminCategoryService
{
    public Task<PagedResult<AdminCategoryDto>> ListAsync(AdminActor actor, AdminCategoryListRequest request, CancellationToken ct)
    {
        Pagination.Validate(request.PageIndex, request.PageSize);
        if (request.Keyword?.Length > 100) throw new ArgumentException("Keyword must be at most 100 characters.");
        var keyword = string.IsNullOrWhiteSpace(request.Keyword) ? null : request.Keyword.Trim();
        var kind = string.IsNullOrWhiteSpace(request.PostCategoryKind) ? null : Kind(request.PostCategoryKind);
        return audit.ExecuteAsync(actor, "category.list", "category", null, async token =>
            Pagination.Map<AdminCategoryProjection, AdminCategoryDto>(await repository.ListAsync(keyword, kind, request.IsActive,
                request.PageIndex, request.PageSize, token), request.PageIndex, request.PageSize, mapper), null, ct);
    }
    public Task<AdminCategoryDto> GetAsync(AdminActor actor, int id, CancellationToken ct) =>
        audit.ExecuteAsync(actor, "category.view", "category", Target(id), token => DetailAsync(id, token), null, ct);

    public Task<AdminCategoryDto> CreateAsync(AdminActor actor, AdminCategoryWriteRequest request, CancellationToken ct)
    {
        var (name, slug, kind) = Validate(request);
        return WriteAsync(() => audit.ExecuteAsync(actor, "category.create", "category", null, async token =>
        {
            await repository.AcquireWriteLockAsync(token);
            await CheckConflictAsync(null, name, slug, token);
            var id = await repository.CreateAsync(new Category { Name = name, Slug = slug, CategoryType = "post", PostCategoryKind = kind, IsActive = true }, token);
            return await DetailAsync(id, token);
        }, result => Target(result.Id), ct));
    }
    public Task<AdminCategoryDto> UpdateAsync(AdminActor actor, int id, AdminCategoryWriteRequest request, CancellationToken ct)
    {
        var (name, slug, kind) = Validate(request);
        return WriteAsync(() => audit.ExecuteAsync(actor, "category.update", "category", Target(id), async token =>
        {
            await repository.AcquireWriteLockAsync(token);
            var category = await repository.LockAsync(id, token) ?? throw new NotFoundException("Post category not found.");
            if (category.PostCategoryKind != kind && await repository.IsReferencedAsync(id, token))
                throw new ConflictException("Cannot change the kind of a category that is in use.");
            await CheckConflictAsync(id, name, slug, token);
            await repository.UpdateAsync(id, name, slug, kind, token);
            return await DetailAsync(id, token);
        }, null, ct));
    }
    public async Task DeleteAsync(AdminActor actor, int id, CancellationToken ct) =>
        await WriteAsync(() => audit.ExecuteAsync(actor, "category.delete", "category", Target(id), async token =>
        {
            await repository.AcquireWriteLockAsync(token);
            _ = await repository.LockAsync(id, token) ?? throw new NotFoundException("Post category not found.");
            if (await repository.IsReferencedAsync(id, token)) throw new ConflictException("Cannot delete a category that is in use.");
            await repository.DeleteAsync(id, token);
            return true;
        }, null, ct));

    private async Task<AdminCategoryDto> DetailAsync(int id, CancellationToken ct) => mapper.Map<AdminCategoryDto>(
        await repository.FindAsync(id, ct) ?? throw new NotFoundException("Post category not found."));
    private async Task CheckConflictAsync(int? exceptId, string name, string slug, CancellationToken ct)
    {
        if (await repository.HasConflictAsync(exceptId, name, slug, ct)) throw new ConflictException("Category name or slug already exists.");
    }
    private static async Task<T> WriteAsync<T>(Func<Task<T>> operation)
    {
        try { return await operation(); }
        catch (CategoryWriteConflictException ex) { throw new ConflictException(ex.Message); }
    }
    private static (string Name, string Slug, string Kind) Validate(AdminCategoryWriteRequest request)
    {
        if (string.IsNullOrWhiteSpace(request.Name) || request.Name.Length > 100) throw new ArgumentException("Name is required and must be at most 100 characters.");
        if (string.IsNullOrWhiteSpace(request.Slug) || request.Slug.Length > 120) throw new ArgumentException("Slug is required and must be at most 120 characters.");
        var slug = request.Slug.Trim().ToLowerInvariant();
        if (!SlugPattern().IsMatch(slug)) throw new ArgumentException("Slug must use letters a-z, digits and single hyphens between words.");
        return (request.Name.Trim(), slug, Kind(request.PostCategoryKind));
    }
    private static string Kind(string value) => value?.Trim().ToLowerInvariant() is "food" or "recipe" or "topic" ? value.Trim().ToLowerInvariant()
        : throw new ArgumentException("PostCategoryKind must be food, recipe or topic.");
    private static string Target(int id) => id > 0 ? id.ToString(CultureInfo.InvariantCulture) : throw new ArgumentException("Invalid category ID.");
    [GeneratedRegex("^[a-z0-9]+(?:-[a-z0-9]+)*$", RegexOptions.CultureInvariant)]
    private static partial Regex SlugPattern();
}
