using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Persistence;

internal static class MealPlanRoutineConfiguration
{
    internal static void Configure(ModelBuilder builder)
    {
        builder.Entity<MealPlanRoutine>(e=>
        {
            e.ToTable("meal_plan_routines",t=>
            {
                t.HasCheckConstraint("CK_routine_mode","\"Mode\" IN ('maintenance','weight_goal')");
                t.HasCheckConstraint("CK_routine_state","\"State\" IN ('active','continued','superseded')");
                t.HasCheckConstraint("CK_routine_goal","(\"Mode\" = 'maintenance' AND \"TargetWeightKg\" IS NULL AND \"TargetDate\" IS NULL AND NOT \"RemindersEnabled\") OR (\"Mode\" = 'weight_goal' AND \"TargetWeightKg\" BETWEEN 30 AND 200 AND \"TargetDate\" IS NOT NULL AND \"EligibilityConfirmed\")");
            });
            e.HasKey(x=>x.MealPlanId);
            e.Property(x=>x.MealPlanId).ValueGeneratedNever();
            e.Property(x=>x.Mode).HasMaxLength(20);
            e.Property(x=>x.State).HasMaxLength(20);
            e.Property(x=>x.TimeZoneId).HasMaxLength(100);
            e.Property(x=>x.TargetWeightKg).HasPrecision(6,2);
            e.Property(x=>x.Version).IsConcurrencyToken();
            e.HasOne<MealPlan>().WithMany().HasForeignKey(x=>x.MealPlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasOne<User>().WithMany().HasForeignKey(x=>x.UserId).OnDelete(DeleteBehavior.NoAction);
            e.HasOne<MealPlan>().WithMany().HasForeignKey(x=>x.PreviousPlanId).OnDelete(DeleteBehavior.NoAction);
            e.HasIndex(x=>new{x.UserId,x.State});
            e.HasIndex(x=>x.PreviousPlanId).IsUnique().HasFilter("\"PreviousPlanId\" IS NOT NULL");
        });
        builder.Entity<MealPlanEntry>(e=>
        {
            e.ToTable("meal_plan_entries",t=>
            {
                t.HasCheckConstraint("CK_entry_status","\"Status\" IN ('unlogged','followed','changed','skipped')");
                t.HasCheckConstraint("CK_entry_slot","\"MealTime\" IN ('breakfast','lunch','dinner')");
                t.HasCheckConstraint("CK_entry_log","(\"Status\" = 'unlogged') = (\"LoggedAt\" IS NULL)");
            });
            e.HasKey(x=>x.Id);
            e.Property(x=>x.Status).HasMaxLength(20);
            e.Property(x=>x.MealTime).HasMaxLength(20);
            e.Property(x=>x.Note).HasMaxLength(500);
            e.HasOne<MealPlan>().WithMany().HasForeignKey(x=>x.MealPlanId).OnDelete(DeleteBehavior.Cascade);
            e.HasIndex(x=>new{x.MealPlanId,x.Date,x.MealTime}).IsUnique();
            e.HasIndex(x=>x.DueAt);
        });
    }
}
