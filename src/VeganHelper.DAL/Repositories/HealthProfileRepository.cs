using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class HealthProfileRepository(AppDbContext db) : IHealthProfileRepository
{
    public async Task<HealthProfileProjection> GetAsync(long userId, CancellationToken ct)
    {
        var profile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        return new(profile, await LoadAllergiesAsync(db, userId, ct));
    }

    internal static Task<List<AllergyProjection>> LoadAllergiesAsync(AppDbContext db, long userId, CancellationToken ct)
    {
        var catalog = from allergy in db.UserAllergies
                      join ingredient in db.Ingredients on allergy.IngredientId equals ingredient.Id
                      where allergy.UserId == userId && !allergy.IsCustom
                      select new AllergyProjection { IngredientId = ingredient.Id, Name = ingredient.Name, IsCustom = false };
        var custom = db.Set<CustomUserAllergy>().Where(x => x.UserId == userId)
            .Select(x => new AllergyProjection { IngredientId = 0L, Name = x.Name, IsCustom = true });
        var legacy = from allergy in db.UserAllergies
                     join ingredient in db.Ingredients on allergy.IngredientId equals ingredient.Id
                     where allergy.UserId == userId && allergy.IsCustom
                     select new AllergyProjection { IngredientId = 0L, Name = ingredient.Name, IsCustom = true };
        return catalog.Concat(custom.Union(legacy)).OrderBy(x => x.Name).ToListAsync(ct);
    }

    public async Task UpdateAsync(long userId, UserProfile values, decimal bmi, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({userId})", ct);
        var profile = await db.UserProfiles.SingleOrDefaultAsync(p => p.UserId == userId, ct);
        if (profile is null)
        {
            var username = await db.Users.Where(u => u.Id == userId).Select(u => u.Username).SingleAsync(ct);
            profile = new UserProfile { UserId = userId, DisplayName = username };
            db.UserProfiles.Add(profile);
        }
        var entry = BodyMeasurements.Apply(profile, values.HeightCm, values.WeightKg);
        if (entry is not null) db.BmiHistories.Add(entry);
        profile.BirthDate = values.BirthDate;
        profile.BiologicalSex = values.BiologicalSex;
        profile.ActivityLevel = values.ActivityLevel;
        profile.DietType = values.DietType;
        profile.UpdatedAt = DateTime.UtcNow;
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task ReplaceAllergiesAsync(long userId, IReadOnlyCollection<long> ingredientIds, IReadOnlyCollection<string> customNames, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({userId})", ct);
        var ids = await db.Ingredients.Where(i => ingredientIds.Contains(i.Id) && !i.IsLegacyAllergyAlias).Select(i => i.Id).ToListAsync(ct);
        if (ids.Count != ingredientIds.Count) throw new ArgumentException("One or more ingredient IDs do not exist.");
        await db.UserAllergies.Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);
        await db.Set<CustomUserAllergy>().Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);
        db.UserAllergies.AddRange(ids.Select(id => new UserAllergy { UserId = userId, IngredientId = id, CreatedAt = DateTime.UtcNow }));
        db.Set<CustomUserAllergy>().AddRange(customNames.Select(name => new CustomUserAllergy { UserId = userId, Name = name }));
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task<DatabasePage<BmiHistory>> GetHistoryAsync(long userId, DateTime since, int pageIndex, int pageSize, CancellationToken ct)
    {
        var query = db.BmiHistories.AsNoTracking().Where(h => h.UserId == userId && h.RecordedAt >= since);
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderBy(h => h.RecordedAt).ThenBy(h => h.Id).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(count, items);
    }

    public async Task<DatabasePage<Ingredient>> SearchIngredientsAsync(string? keyword, int pageIndex, int pageSize, CancellationToken ct)
    {
        var query = db.Ingredients.AsNoTracking().Where(i => !i.IsLegacyAllergyAlias);
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.ToLowerInvariant();
            query = query.Where(i => i.Name.ToLower().Contains(normalized));
        }
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderBy(i => i.Name).ThenBy(i => i.Id).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(count, items);
    }

    public async Task<IReadOnlyList<Ingredient>?> GetPostIngredientsAsync(long userId, long postId, CancellationToken ct)
    {
        if (!await db.Posts.AnyAsync(p => p.Id == postId && !p.IsDeleted &&
            ((p.Status == "published" && !p.ModerationHold) || p.AuthorId == userId), ct)) return null;
        return await (from ingredient in db.Ingredients.AsNoTracking()
                      join link in db.PostIngredients on ingredient.Id equals link.IngredientId
                      where link.PostId == postId
                      orderby ingredient.Name, ingredient.Id
                      select ingredient).ToListAsync(ct);
    }
}
