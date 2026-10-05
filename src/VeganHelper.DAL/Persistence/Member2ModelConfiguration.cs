using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Persistence;

internal static class Member2ModelConfiguration
{
    internal static void Configure(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<UserAllergy>().Property(a => a.IsCustom).HasColumnName("is_custom").HasDefaultValue(false);
        modelBuilder.Entity<Shop>(entity =>
        {
            entity.Property(s => s.Description).HasColumnName("description").HasColumnType("text");
            entity.Property(s => s.ContactPhone).HasColumnName("contact_phone").HasMaxLength(30);
            entity.Property(s => s.Rating).HasColumnName("rating").HasPrecision(3, 2);
            entity.Property(s => s.OpeningHours).HasColumnName("opening_hours").HasMaxLength(1000);
            entity.ToTable("shops", table => table.HasCheckConstraint("CK_shops_member2_rating", "rating IS NULL OR rating BETWEEN 0 AND 5"));
            entity.HasIndex(s => new { s.IsApproved, s.IsDeleted, s.Latitude, s.Longitude }).HasDatabaseName("IX_shops_member2_location");
        });
        modelBuilder.Entity<ShopOpeningPeriod>(entity =>
        {
            entity.ToTable("shop_opening_periods", table => table.HasCheckConstraint("CK_shop_opening_periods_day", "day_of_week BETWEEN 1 AND 7"));
            entity.HasKey(p => p.Id);
            entity.Property(p => p.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(p => p.ShopId).HasColumnName("shop_id");
            entity.Property(p => p.DayOfWeek).HasColumnName("day_of_week");
            entity.Property(p => p.OpensAt).HasColumnName("opens_at");
            entity.Property(p => p.ClosesAt).HasColumnName("closes_at");
            entity.Property(p => p.IsClosed).HasColumnName("is_closed");
            entity.HasOne<Shop>().WithMany(s => s.OpeningPeriods).HasForeignKey(p => p.ShopId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(p => new { p.ShopId, p.DayOfWeek });
        });
        modelBuilder.Entity<ShopMedia>(entity =>
        {
            entity.ToTable("shop_media", table => table.HasCheckConstraint("CK_shop_media_order", "display_order >= 0"));
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(m => m.ShopId).HasColumnName("shop_id");
            entity.Property(m => m.MediaUrl).HasColumnName("media_url").HasMaxLength(2048).IsRequired();
            entity.Property(m => m.DisplayOrder).HasColumnName("display_order");
            entity.HasOne<Shop>().WithMany(s => s.Media).HasForeignKey(m => m.ShopId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(m => new { m.ShopId, m.DisplayOrder });
        });
        modelBuilder.Entity<ShopMenuItem>(entity =>
        {
            entity.ToTable("shop_menu_items");
            entity.HasKey(m => m.Id);
            entity.Property(m => m.Id).HasColumnName("id").UseIdentityByDefaultColumn();
            entity.Property(m => m.ShopId).HasColumnName("shop_id");
            entity.Property(m => m.Name).HasColumnName("name").HasMaxLength(255).IsRequired();
            entity.HasOne<Shop>().WithMany(s => s.MenuItems).HasForeignKey(m => m.ShopId).OnDelete(DeleteBehavior.Cascade);
            entity.HasIndex(m => new { m.ShopId, m.Name }).IsUnique();
        });
    }
}
