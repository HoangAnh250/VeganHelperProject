using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using VeganHelper.DAL.Entities;
using VeganHelper.DAL.Persistence;

namespace VeganHelper.DAL.Repositories;

public interface IMealRoutineRepository
{
    Task<IDbContextTransaction> BeginAsync(CancellationToken ct);
    Task LockProfileAsync(long userId,CancellationToken ct);
    Task<MealPlanRoutine?> GetAsync(long userId,long planId,bool locked,CancellationToken ct);
    Task<List<MealPlanRoutine>> GetManyAsync(long userId,IReadOnlyCollection<long> planIds,CancellationToken ct);
    Task<List<MealPlanEntry>> EntriesAsync(long planId,CancellationToken ct);
    Task<List<MealPlanEntry>> EntriesAsync(IReadOnlyCollection<long> planIds,CancellationToken ct);
    Task AddAsync(MealPlanRoutine routine,IEnumerable<MealPlanEntry> entries,CancellationToken ct);
    Task ReplaceEntriesAsync(long planId,IEnumerable<MealPlanEntry> entries,CancellationToken ct);
    Task SaveAsync(CancellationToken ct);
    Task<List<(MealPlanRoutine Routine,MealPlan Plan)>> ActiveAsync(int offset,int limit,CancellationToken ct);
    Task<(DateOnly Start,List<MealPlanEntry> Entries)> ReviewHistoryAsync(long rootId,CancellationToken ct);
}

public sealed class MealRoutineRepository(AppDbContext db) : IMealRoutineRepository
{
    public async Task<(DateOnly Start,List<MealPlanEntry> Entries)> ReviewHistoryAsync(long rootId,CancellationToken ct)
    {
        var start=await db.MealPlans.Where(p=>p.Id==rootId).Select(p=>p.StartDate).SingleAsync(ct);
        var latest=from e in db.Set<MealPlanEntry>() join r in db.Set<MealPlanRoutine>() on e.MealPlanId equals r.MealPlanId
            where r.RootPlanId==rootId group e by new {e.Date,e.MealTime} into g select g.Max(e=>e.Id);
        var entries=await db.Set<MealPlanEntry>().AsNoTracking().Where(e=>latest.Contains(e.Id)).ToListAsync(ct);
        return (start!.Value,entries);
    }
    public Task<IDbContextTransaction> BeginAsync(CancellationToken ct)=>db.Database.BeginTransactionAsync(ct);
    // Same user lock as HealthProfileRepository.UpdateAsync / ReplaceAllergiesAsync.
    public Task LockProfileAsync(long userId,CancellationToken ct)=>
        db.Database.ExecuteSqlInterpolatedAsync($"SELECT pg_advisory_xact_lock({userId})",ct);
    public Task<MealPlanRoutine?> GetAsync(long userId,long planId,bool locked,CancellationToken ct)
    {
        var query=locked
            ? db.Set<MealPlanRoutine>().FromSqlInterpolated($"SELECT * FROM meal_plan_routines WHERE \"MealPlanId\" = {planId} AND \"UserId\" = {userId} FOR UPDATE")
            : db.Set<MealPlanRoutine>().Where(r=>r.MealPlanId==planId && r.UserId==userId);
        return query.SingleOrDefaultAsync(ct);
    }
    public Task<List<MealPlanRoutine>> GetManyAsync(long userId,IReadOnlyCollection<long> planIds,CancellationToken ct)=>
        db.Set<MealPlanRoutine>().AsNoTracking()
            .Where(r=>r.UserId==userId && planIds.Contains(r.MealPlanId)).ToListAsync(ct);
    public Task<List<MealPlanEntry>> EntriesAsync(long planId,CancellationToken ct)=>
        db.Set<MealPlanEntry>().Where(e=>e.MealPlanId==planId).OrderBy(e=>e.Date).ThenBy(e=>e.MealTime).ToListAsync(ct);
    public Task<List<MealPlanEntry>> EntriesAsync(IReadOnlyCollection<long> planIds,CancellationToken ct)=>
        db.Set<MealPlanEntry>().AsNoTracking().Where(e=>planIds.Contains(e.MealPlanId))
            .OrderBy(e=>e.MealPlanId).ThenBy(e=>e.Date).ThenBy(e=>e.MealTime).ToListAsync(ct);
    public async Task AddAsync(MealPlanRoutine routine,IEnumerable<MealPlanEntry> entries,CancellationToken ct)
    {
        db.Add(routine); db.AddRange(entries); await db.SaveChangesAsync(ct);
    }
    public async Task ReplaceEntriesAsync(long planId,IEnumerable<MealPlanEntry> entries,CancellationToken ct)
    {
        await db.Set<MealPlanEntry>().Where(entry=>entry.MealPlanId==planId).ExecuteDeleteAsync(ct);
        db.AddRange(entries);
    }
    public Task SaveAsync(CancellationToken ct)=>db.SaveChangesAsync(ct);
    public async Task<List<(MealPlanRoutine Routine,MealPlan Plan)>> ActiveAsync(int offset,int limit,CancellationToken ct)
    {
        var rows=await (from r in db.Set<MealPlanRoutine>().AsNoTracking()
            join p in db.MealPlans.AsNoTracking() on r.MealPlanId equals p.Id
            where (r.State=="active" || r.State=="continued") && r.Mode=="weight_goal"
            orderby r.MealPlanId select new {Routine=r,Plan=p}).Skip(offset).Take(limit).ToListAsync(ct);
        return rows.Select(r=>(r.Routine,r.Plan)).ToList();
    }
}
