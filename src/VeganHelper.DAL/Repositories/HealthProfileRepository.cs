using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public sealed class HealthProfileRepository(AppDbContext db) : IHealthProfileRepository
{
    public async Task<HealthProfileProjection> GetAsync(long userId, CancellationToken ct)
    {
        var profile = await db.UserProfiles.AsNoTracking().SingleOrDefaultAsync(p => p.UserId == userId, ct);
        var allergies = await (from allergy in db.UserAllergies.AsNoTracking()
                               join ingredient in db.Ingredients on allergy.IngredientId equals ingredient.Id
                               where allergy.UserId == userId
                               orderby ingredient.Name, ingredient.Id
                               select new AllergyProjection { IngredientId = ingredient.Id, Name = ingredient.Name, IsCustom = allergy.IsCustom }).ToListAsync(ct);
        return new(profile, allergies);
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
        profile.HeightCm = values.HeightCm;
        profile.WeightKg = values.WeightKg;
        profile.BirthDate = values.BirthDate;
        profile.BiologicalSex = values.BiologicalSex;
        profile.ActivityLevel = values.ActivityLevel;
        profile.DietType = values.DietType;
        profile.CurrentBmi = bmi;
        profile.UpdatedAt = DateTime.UtcNow;
        db.BmiHistories.Add(new BmiHistory { UserId = userId, HeightCm = values.HeightCm!.Value, WeightKg = values.WeightKg!.Value, BmiValue = bmi, RecordedAt = DateTime.UtcNow });
        await db.SaveChangesAsync(ct);
        await transaction.CommitAsync(ct);
    }

    public async Task ReplaceAllergiesAsync(long userId, IReadOnlyCollection<long> ingredientIds, IReadOnlyCollection<string> customNames, CancellationToken ct)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(ct);
        await db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({userId})", ct);
        var ids = await db.Ingredients.Where(i => ingredientIds.Contains(i.Id)).Select(i => i.Id).ToListAsync(ct);
        if (ids.Count != ingredientIds.Count) throw new ArgumentException("One or more ingredient IDs do not exist.");
        var selected = ids.ToDictionary(id => id, _ => false);
        if (customNames.Count > 0) await db.Database.ExecuteSqlRawAsync("SELECT pg_advisory_xact_lock(-3912301)", ct);
        foreach (var name in customNames)
        {
            var normalized = name.ToLowerInvariant();
            var ingredient = await db.Ingredients.FirstOrDefaultAsync(i => i.Name.ToLower() == normalized, ct);
            if (ingredient is null)
            {
                var inserted = await db.Ingredients.FromSqlInterpolated($"""
                    INSERT INTO ingredients (name, default_unit) VALUES ({name}, {"g"})
                    ON CONFLICT (name) DO UPDATE SET name = EXCLUDED.name RETURNING *
                    """).ToListAsync(ct);
                ingredient = inserted.Single();
            }
            selected.TryAdd(ingredient.Id, true);
        }
        await db.UserAllergies.Where(a => a.UserId == userId).ExecuteDeleteAsync(ct);
        db.UserAllergies.AddRange(selected.Select(pair => new UserAllergy { UserId = userId, IngredientId = pair.Key, IsCustom = pair.Value, CreatedAt = DateTime.UtcNow }));
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
        var query = db.Ingredients.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(keyword))
        {
            var normalized = keyword.ToLowerInvariant();
            query = query.Where(i => i.Name.ToLower().Contains(normalized));
        }
        var count = await query.LongCountAsync(ct);
        var items = await query.OrderBy(i => i.Name).ThenBy(i => i.Id).Skip((pageIndex - 1) * pageSize).Take(pageSize).ToListAsync(ct);
        return new(count, items);
    }

    public async Task<IReadOnlyList<AllergyProjection>?> GetPostWarningsAsync(long userId, long postId, CancellationToken ct)
    {
        if (!await db.Posts.AnyAsync(p => p.Id == postId && !p.IsDeleted && (p.Status == "published" || p.AuthorId == userId), ct)) return null;
        return await (from ingredient in db.Ingredients.AsNoTracking()
                      join allergy in db.UserAllergies on ingredient.Id equals allergy.IngredientId
                      join postIngredient in db.PostIngredients on ingredient.Id equals postIngredient.IngredientId
                      where allergy.UserId == userId && postIngredient.PostId == postId
                      orderby ingredient.Name, ingredient.Id
                      select new AllergyProjection { IngredientId = ingredient.Id, Name = ingredient.Name, IsCustom = allergy.IsCustom }).ToListAsync(ct);
    }
}
