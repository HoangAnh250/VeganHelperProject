using Microsoft.EntityFrameworkCore;
using VeganHelper.DAL.Entities;

namespace VeganHelper.DAL.Persistence;

public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();
    public DbSet<EmailVerificationToken> EmailVerificationTokens => Set<EmailVerificationToken>();
    public DbSet<PasswordResetToken> PasswordResetTokens => Set<PasswordResetToken>();
    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();
    public DbSet<UserProfile> UserProfiles => Set<UserProfile>();
    public DbSet<Role> Roles => Set<Role>();
    public DbSet<UserIdentity> UserIdentities => Set<UserIdentity>();
    public DbSet<Post> Posts => Set<Post>();
    public DbSet<PostMedia> PostMedia => Set<PostMedia>();
    public DbSet<PostSummary> PostSummaries => Set<PostSummary>();
    public DbSet<Category> Categories => Set<Category>();
    public DbSet<PostCategory> PostCategories => Set<PostCategory>();
    public DbSet<Ingredient> Ingredients => Set<Ingredient>();
    public DbSet<PostIngredient> PostIngredients => Set<PostIngredient>();
    public DbSet<UserAllergy> UserAllergies => Set<UserAllergy>();
    public DbSet<UserAvailableIngredient> UserAvailableIngredients => Set<UserAvailableIngredient>();
    public DbSet<PostLike> PostLikes => Set<PostLike>();
    public DbSet<SavedPost> SavedPosts => Set<SavedPost>();
    public DbSet<Comment> Comments => Set<Comment>();
    public DbSet<MealPlan> MealPlans => Set<MealPlan>();
    public DbSet<Meal> Meals => Set<Meal>();
    public DbSet<MealPlanSchedule> MealPlanSchedule => Set<MealPlanSchedule>();
    public DbSet<MealIngredient> MealIngredients => Set<MealIngredient>();
    public DbSet<ChatSession> ChatSessions => Set<ChatSession>();
    public DbSet<ChatMessage> ChatMessages => Set<ChatMessage>();
    public DbSet<AiUsage> AiUsage => Set<AiUsage>();
    public DbSet<PostEmbedding> PostEmbeddings => Set<PostEmbedding>();
    public DbSet<Shop> Shops => Set<Shop>();
    public DbSet<ShopCategory> ShopCategories => Set<ShopCategory>();
    public DbSet<Flag> Flags => Set<Flag>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<User>(entity =>
        {
            entity.ToTable("users", table =>
            {
                table.HasCheckConstraint("CK_users_1", "deleted_at IS NULL OR is_active = 0");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_users");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.Username).HasColumnName("username").HasColumnType("NVARCHAR(100)").IsRequired(true);
            entity.HasAlternateKey(x => x.Username).HasName("UQ_users_username");
            entity.Property(x => x.Email).HasColumnName("email").HasColumnType("NVARCHAR(255)").IsRequired(true);
            entity.HasAlternateKey(x => x.Email).HasName("UQ_users_email");
            entity.Property(x => x.PhoneNumber).HasColumnName("phone_number").HasColumnType("NVARCHAR(20)").IsRequired(false);
            entity.Property(x => x.PasswordHash).HasColumnName("password_hash").HasColumnType("NVARCHAR(500)").IsRequired(false);
            entity.Property(x => x.EmailVerifiedAt).HasColumnName("email_verified_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("BIT").IsRequired(true).HasDefaultValueSql("1");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.LastLoginAt).HasColumnName("last_login_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.FailedLoginAttempts).HasColumnName("failed_login_attempts").HasColumnType("INT").IsRequired(true).HasDefaultValueSql("0");
            entity.Property(x => x.LockedUntil).HasColumnName("locked_until").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.RoleId).HasColumnName("role_id").HasColumnType("INT").IsRequired(true);
            entity.HasOne<Role>().WithMany().HasForeignKey(x => new { x.RoleId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_users_1");
        });
        modelBuilder.Entity<EmailVerificationToken>(entity =>
        {
            entity.ToTable("email_verification_tokens");
            entity.HasKey(x => x.Id).HasName("PK_email_verification_tokens");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").UseIdentityColumn();
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired();
            entity.Property(x => x.TokenHash).HasColumnName("token_hash").HasColumnType("VARCHAR(128)").IsRequired();
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("DATETIME2").IsRequired();
            entity.Property(x => x.ConsumedAt).HasColumnName("consumed_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_email_verification_tokens_users");
            entity.HasIndex(x => new { x.UserId, x.ExpiresAt }).HasDatabaseName("IX_email_verification_tokens_user_expiry");
            entity.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UQ_email_verification_tokens_hash");
        });
        modelBuilder.Entity<PasswordResetToken>(entity =>
        {
            entity.ToTable("password_reset_tokens");
            entity.HasKey(x => x.Id).HasName("PK_password_reset_tokens");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").UseIdentityColumn();
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired();
            entity.Property(x => x.TokenHash).HasColumnName("token_hash").HasColumnType("VARCHAR(128)").IsRequired();
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("DATETIME2").IsRequired();
            entity.Property(x => x.UsedAt).HasColumnName("used_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_password_reset_tokens_users");
            entity.HasIndex(x => new { x.UserId, x.ExpiresAt }).HasDatabaseName("IX_password_reset_tokens_user_expiry");
            entity.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UQ_password_reset_tokens_hash");
        });
        modelBuilder.Entity<RefreshToken>(entity =>
        {
            entity.ToTable("refresh_tokens");
            entity.HasKey(x => x.Id).HasName("PK_refresh_tokens");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").UseIdentityColumn();
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired();
            entity.Property(x => x.TokenHash).HasColumnName("token_hash").HasColumnType("VARCHAR(128)").IsRequired();
            entity.Property(x => x.ExpiresAt).HasColumnName("expires_at").HasColumnType("DATETIME2").IsRequired();
            entity.Property(x => x.RevokedAt).HasColumnName("revoked_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired().HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<User>().WithMany().HasForeignKey(x => x.UserId).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_refresh_tokens_users");
            entity.HasIndex(x => new { x.UserId, x.ExpiresAt, x.RevokedAt }).HasDatabaseName("IX_refresh_tokens_user_status");
            entity.HasIndex(x => x.TokenHash).IsUnique().HasDatabaseName("UQ_refresh_tokens_hash");
        });
        modelBuilder.Entity<UserProfile>(entity =>
        {
            entity.ToTable("user_profiles", table =>
            {
                table.HasCheckConstraint("CK_user_profiles_1", "height_cm IS NULL OR height_cm > 0");
                table.HasCheckConstraint("CK_user_profiles_2", "weight_kg IS NULL OR weight_kg > 0");
                table.HasCheckConstraint("CK_user_profiles_3", "biological_sex IN ('male','female','other')");
                table.HasCheckConstraint("CK_user_profiles_4", "diet_type IN ('vegan','lacto_ovo_vegetarian')");
            });
            entity.HasKey(x => new { x.UserId }).HasName("PK_user_profiles");
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.DisplayName).HasColumnName("display_name").HasColumnType("NVARCHAR(100)").IsRequired(true);
            entity.Property(x => x.AvatarUrl).HasColumnName("avatar_url").HasColumnType("NVARCHAR(1000)").IsRequired(false);
            entity.Property(x => x.HeightCm).HasColumnName("height_cm").HasColumnType("DECIMAL(5,2)").IsRequired(false);
            entity.Property(x => x.WeightKg).HasColumnName("weight_kg").HasColumnType("DECIMAL(6,2)").IsRequired(false);
            entity.Property(x => x.BirthDate).HasColumnName("birth_date").HasColumnType("DATE").IsRequired(false);
            entity.Property(x => x.BiologicalSex).HasColumnName("biological_sex").HasColumnType("NVARCHAR(20)").IsRequired(false);
            entity.Property(x => x.DietType).HasColumnName("diet_type").HasColumnType("NVARCHAR(30)").IsRequired(true).HasDefaultValueSql("'vegan'");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.UserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_user_profiles_1");
        });
        modelBuilder.Entity<Role>(entity =>
        {
            entity.ToTable("roles", table =>
            {
                table.HasCheckConstraint("CK_roles_1", "role_name IN ('member','admin')");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_roles");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("INT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.RoleName).HasColumnName("role_name").HasColumnType("NVARCHAR(50)").IsRequired(true);
            entity.HasAlternateKey(x => x.RoleName).HasName("UQ_roles_role_name");
        });
        modelBuilder.Entity<UserIdentity>(entity =>
        {
            entity.ToTable("user_identities", table =>
            {
                table.HasCheckConstraint("CK_user_identities_1", "provider IN ('google')");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_user_identities");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired(true);
            entity.Property(x => x.Provider).HasColumnName("provider").HasColumnType("NVARCHAR(20)").IsRequired(true).HasDefaultValueSql("'google'");
            entity.Property(x => x.ProviderSubject).HasColumnName("provider_subject").HasColumnType("NVARCHAR(255)").IsRequired(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasAlternateKey(x => new { x.Provider, x.ProviderSubject }).HasName("UQ_user_identities_1");
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.UserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_user_identities_1");
        });
        modelBuilder.Entity<Post>(entity =>
        {
            entity.ToTable("posts", table =>
            {
                table.HasCheckConstraint("CK_posts_1", "post_type IN ('article','video','recipe','community')");
                table.HasCheckConstraint("CK_posts_2", "meal_type IN ('breakfast','lunch','dinner','snack')");
                table.HasCheckConstraint("CK_posts_3", "diet_type IN ('vegan','lacto_ovo_vegetarian')");
                table.HasCheckConstraint("CK_posts_4", "status IN ('draft','pending_review','published','rejected','hidden')");
                table.HasCheckConstraint("CK_posts_5", "prep_time_mins >= 0");
                table.HasCheckConstraint("CK_posts_6", "cooking_time_mins >= 0");
                table.HasCheckConstraint("CK_posts_7", "servings > 0");
                table.HasCheckConstraint("CK_posts_8", "calories_per_serving >= 0");
                table.HasCheckConstraint("CK_posts_9", "view_count >= 0");
                table.HasCheckConstraint("CK_posts_10", "(is_deleted = 0 AND deleted_at IS NULL) OR (is_deleted = 1 AND deleted_at IS NOT NULL)");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_posts");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.AuthorId).HasColumnName("author_id").HasColumnType("BIGINT").IsRequired(true);
            entity.Property(x => x.PostType).HasColumnName("post_type").HasColumnType("NVARCHAR(20)").IsRequired(true);
            entity.Property(x => x.Title).HasColumnName("title").HasColumnType("NVARCHAR(255)").IsRequired(true);
            entity.Property(x => x.Content).HasColumnName("content").HasColumnType("NVARCHAR(MAX)").IsRequired(false);
            entity.Property(x => x.MealType).HasColumnName("meal_type").HasColumnType("NVARCHAR(20)").IsRequired(false);
            entity.Property(x => x.PrepTimeMins).HasColumnName("prep_time_mins").HasColumnType("INT").IsRequired(false);
            entity.Property(x => x.CookingTimeMins).HasColumnName("cooking_time_mins").HasColumnType("INT").IsRequired(false);
            entity.Property(x => x.Servings).HasColumnName("servings").HasColumnType("INT").IsRequired(false);
            entity.Property(x => x.CaloriesPerServing).HasColumnName("calories_per_serving").HasColumnType("DECIMAL(10,2)").IsRequired(false);
            entity.Property(x => x.DietType).HasColumnName("diet_type").HasColumnType("NVARCHAR(30)").IsRequired(false);
            entity.Property(x => x.IngredientsVerified).HasColumnName("ingredients_verified").HasColumnType("BIT").IsRequired(true).HasDefaultValueSql("0");
            entity.Property(x => x.Status).HasColumnName("status").HasColumnType("NVARCHAR(20)").IsRequired(true).HasDefaultValueSql("'draft'");
            entity.Property(x => x.ViewCount).HasColumnName("view_count").HasColumnType("BIGINT").IsRequired(true).HasDefaultValueSql("0");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasColumnType("BIT").IsRequired(true).HasDefaultValueSql("0");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.AuthorId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_posts_1");
            entity.HasIndex(x => new { x.Status, x.IsDeleted, x.CreatedAt }).HasDatabaseName("IX_posts_1");
            entity.HasIndex(x => new { x.AuthorId, x.CreatedAt }).HasDatabaseName("IX_posts_2");
        });
        modelBuilder.Entity<PostMedia>(entity =>
        {
            entity.ToTable("post_media", table =>
            {
                table.HasCheckConstraint("CK_post_media_1", "media_type IN ('image','video')");
                table.HasCheckConstraint("CK_post_media_2", "processing_status IN ('uploading','processing','ready','failed')");
                table.HasCheckConstraint("CK_post_media_3", "duration_seconds >= 0");
                table.HasCheckConstraint("CK_post_media_4", "display_order >= 0");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_post_media");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.PostId).HasColumnName("post_id").HasColumnType("BIGINT").IsRequired(true);
            entity.Property(x => x.MediaUrl).HasColumnName("media_url").HasColumnType("NVARCHAR(1000)").IsRequired(true);
            entity.Property(x => x.MediaType).HasColumnName("media_type").HasColumnType("NVARCHAR(20)").IsRequired(true);
            entity.Property(x => x.CloudPublicId).HasColumnName("cloud_public_id").HasColumnType("NVARCHAR(255)").IsRequired(false);
            entity.Property(x => x.ThumbnailUrl).HasColumnName("thumbnail_url").HasColumnType("NVARCHAR(1000)").IsRequired(false);
            entity.Property(x => x.DurationSeconds).HasColumnName("duration_seconds").HasColumnType("INT").IsRequired(false);
            entity.Property(x => x.IsPrimary).HasColumnName("is_primary").HasColumnType("BIT").IsRequired(true).HasDefaultValueSql("0");
            entity.Property(x => x.DisplayOrder).HasColumnName("display_order").HasColumnType("INT").IsRequired(true).HasDefaultValueSql("0");
            entity.Property(x => x.ProcessingStatus).HasColumnName("processing_status").HasColumnType("NVARCHAR(20)").IsRequired(true).HasDefaultValueSql("'ready'");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasAlternateKey(x => new { x.PostId, x.Id }).HasName("UQ_post_media_1");
            entity.HasOne<Post>().WithMany().HasForeignKey(x => new { x.PostId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_post_media_1");
            entity.HasIndex(x => new { x.PostId }).HasDatabaseName("IX_post_media_1").IsUnique().HasFilter("is_primary = 1");
        });
        modelBuilder.Entity<PostSummary>(entity =>
        {
            entity.ToTable("post_summaries", table =>
            {
                table.HasCheckConstraint("CK_post_summaries_1", "status IN ('pending','processing','completed','failed','stale')");
                table.HasCheckConstraint("CK_post_summaries_2", "status <> 'completed' OR (transcript IS NOT NULL AND ai_generated_text IS NOT NULL AND generated_at IS NOT NULL)");
            });
            entity.HasKey(x => new { x.PostId }).HasName("PK_post_summaries");
            entity.Property(x => x.PostId).HasColumnName("post_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.SourceMediaId).HasColumnName("source_media_id").HasColumnType("BIGINT").IsRequired(true);
            entity.Property(x => x.Transcript).HasColumnName("transcript").HasColumnType("NVARCHAR(MAX)").IsRequired(false);
            entity.Property(x => x.AiGeneratedText).HasColumnName("ai_generated_text").HasColumnType("NVARCHAR(MAX)").IsRequired(false);
            entity.Property(x => x.Status).HasColumnName("status").HasColumnType("NVARCHAR(20)").IsRequired(true).HasDefaultValueSql("'pending'");
            entity.Property(x => x.ModelName).HasColumnName("model_name").HasColumnType("NVARCHAR(100)").IsRequired(false);
            entity.Property(x => x.ErrorMessage).HasColumnName("error_message").HasColumnType("NVARCHAR(2000)").IsRequired(false);
            entity.Property(x => x.RequestedAt).HasColumnName("requested_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.GeneratedAt).HasColumnName("generated_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.HasOne<Post>().WithMany().HasForeignKey(x => new { x.PostId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_post_summaries_1");
            entity.HasOne<PostMedia>().WithMany().HasForeignKey(x => new { x.PostId, x.SourceMediaId }).HasPrincipalKey(x => new { x.PostId, x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_post_summaries_2");
        });
        modelBuilder.Entity<Category>(entity =>
        {
            entity.ToTable("categories", table =>
            {
                table.HasCheckConstraint("CK_categories_1", "category_type IN ('post','shop')");
                table.HasCheckConstraint("CK_categories_2", "(category_type = 'shop' AND post_category_kind IS NULL) OR (category_type = 'post' AND post_category_kind IS NOT NULL AND post_category_kind IN ('food','recipe','topic'))");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_categories");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("INT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.Name).HasColumnName("name").HasColumnType("NVARCHAR(100)").IsRequired(true);
            entity.Property(x => x.Slug).HasColumnName("slug").HasColumnType("NVARCHAR(120)").IsRequired(true);
            entity.HasAlternateKey(x => x.Slug).HasName("UQ_categories_slug");
            entity.Property(x => x.CategoryType).HasColumnName("category_type").HasColumnType("NVARCHAR(20)").IsRequired(true);
            entity.Property(x => x.PostCategoryKind).HasColumnName("post_category_kind").HasColumnType("NVARCHAR(20)").IsRequired(false);
            entity.Property(x => x.IsActive).HasColumnName("is_active").HasColumnType("BIT").IsRequired(true).HasDefaultValueSql("1");
            entity.HasAlternateKey(x => new { x.CategoryType, x.Name }).HasName("UQ_categories_1");
        });
        modelBuilder.Entity<PostCategory>(entity =>
        {
            entity.ToTable("post_categories", table =>
            {
            });
            entity.HasKey(x => new { x.PostId, x.CategoryId }).HasName("PK_post_categories");
            entity.Property(x => x.PostId).HasColumnName("post_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.CategoryId).HasColumnName("category_id").HasColumnType("INT").IsRequired(true).ValueGeneratedNever();
            entity.HasOne<Post>().WithMany().HasForeignKey(x => new { x.PostId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_post_categories_1");
            entity.HasOne<Category>().WithMany().HasForeignKey(x => new { x.CategoryId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_post_categories_2");
        });
        modelBuilder.Entity<Ingredient>(entity =>
        {
            entity.ToTable("ingredients", table =>
            {
                table.HasCheckConstraint("CK_ingredients_1", "calories_per_100g >= 0");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_ingredients");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.Name).HasColumnName("name").HasColumnType("NVARCHAR(100)").IsRequired(true);
            entity.HasAlternateKey(x => x.Name).HasName("UQ_ingredients_name");
            entity.Property(x => x.DefaultUnit).HasColumnName("default_unit").HasColumnType("NVARCHAR(20)").IsRequired(true);
            entity.Property(x => x.CaloriesPer100g).HasColumnName("calories_per_100g").HasColumnType("DECIMAL(10,2)").IsRequired(false);
        });
        modelBuilder.Entity<PostIngredient>(entity =>
        {
            entity.ToTable("post_ingredients", table =>
            {
                table.HasCheckConstraint("CK_post_ingredients_1", "quantity > 0");
            });
            entity.HasKey(x => new { x.PostId, x.IngredientId }).HasName("PK_post_ingredients");
            entity.Property(x => x.PostId).HasColumnName("post_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.IngredientId).HasColumnName("ingredient_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("DECIMAL(12,3)").IsRequired(false);
            entity.Property(x => x.Unit).HasColumnName("unit").HasColumnType("NVARCHAR(20)").IsRequired(true);
            entity.HasOne<Post>().WithMany().HasForeignKey(x => new { x.PostId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_post_ingredients_1");
            entity.HasOne<Ingredient>().WithMany().HasForeignKey(x => new { x.IngredientId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_post_ingredients_2");
        });
        modelBuilder.Entity<UserAllergy>(entity =>
        {
            entity.ToTable("user_allergies", table =>
            {
            });
            entity.HasKey(x => new { x.UserId, x.IngredientId }).HasName("PK_user_allergies");
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.IngredientId).HasColumnName("ingredient_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.UserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_user_allergies_1");
            entity.HasOne<Ingredient>().WithMany().HasForeignKey(x => new { x.IngredientId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_user_allergies_2");
        });
        modelBuilder.Entity<UserAvailableIngredient>(entity =>
        {
            entity.ToTable("user_available_ingredients", table =>
            {
                table.HasCheckConstraint("CK_user_available_ingredients_1", "quantity >= 0");
            });
            entity.HasKey(x => new { x.UserId, x.IngredientId }).HasName("PK_user_available_ingredients");
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.IngredientId).HasColumnName("ingredient_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.Quantity).HasColumnName("quantity").HasColumnType("DECIMAL(12,3)").IsRequired(false);
            entity.Property(x => x.Unit).HasColumnName("unit").HasColumnType("NVARCHAR(20)").IsRequired(true);
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.UserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_user_available_ingredients_1");
            entity.HasOne<Ingredient>().WithMany().HasForeignKey(x => new { x.IngredientId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_user_available_ingredients_2");
        });
        modelBuilder.Entity<PostLike>(entity =>
        {
            entity.ToTable("post_likes", table =>
            {
            });
            entity.HasKey(x => new { x.UserId, x.PostId }).HasName("PK_post_likes");
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.PostId).HasColumnName("post_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.UserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_post_likes_1");
            entity.HasOne<Post>().WithMany().HasForeignKey(x => new { x.PostId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_post_likes_2");
        });
        modelBuilder.Entity<SavedPost>(entity =>
        {
            entity.ToTable("saved_posts", table =>
            {
            });
            entity.HasKey(x => new { x.UserId, x.PostId }).HasName("PK_saved_posts");
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.PostId).HasColumnName("post_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.SavedAt).HasColumnName("saved_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.UserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_saved_posts_1");
            entity.HasOne<Post>().WithMany().HasForeignKey(x => new { x.PostId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_saved_posts_2");
        });
        modelBuilder.Entity<Comment>(entity =>
        {
            entity.ToTable("comments", table =>
            {
                table.HasCheckConstraint("CK_comments_1", "parent_comment_id IS NULL OR parent_comment_id <> id");
                table.HasCheckConstraint("CK_comments_2", "status IN ('visible','hidden')");
                table.HasCheckConstraint("CK_comments_3", "(is_deleted = 0 AND deleted_at IS NULL) OR (is_deleted = 1 AND deleted_at IS NOT NULL)");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_comments");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.PostId).HasColumnName("post_id").HasColumnType("BIGINT").IsRequired(true);
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired(true);
            entity.Property(x => x.ParentCommentId).HasColumnName("parent_comment_id").HasColumnType("BIGINT").IsRequired(false);
            entity.Property(x => x.Content).HasColumnName("content").HasColumnType("NVARCHAR(1000)").IsRequired(true);
            entity.Property(x => x.Status).HasColumnName("status").HasColumnType("NVARCHAR(20)").IsRequired(true).HasDefaultValueSql("'visible'");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasColumnType("BIT").IsRequired(true).HasDefaultValueSql("0");
            entity.Property(x => x.DeletedAt).HasColumnName("deleted_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.HasAlternateKey(x => new { x.PostId, x.Id }).HasName("UQ_comments_1");
            entity.HasOne<Post>().WithMany().HasForeignKey(x => new { x.PostId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_comments_1");
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.UserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_comments_2");
            entity.HasOne<Comment>().WithMany().HasForeignKey(x => new { x.PostId, x.ParentCommentId }).HasPrincipalKey(x => new { x.PostId, x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_comments_3");
            entity.HasIndex(x => new { x.PostId, x.CreatedAt }).HasDatabaseName("IX_comments_1");
        });
        modelBuilder.Entity<MealPlan>(entity =>
        {
            entity.ToTable("meal_plans", table =>
            {
                table.HasCheckConstraint("CK_meal_plans_1", "DATEDIFF(DAY, start_date, end_date) = 6");
                table.HasCheckConstraint("CK_meal_plans_2", "height_cm > 0 AND weight_kg > 0 AND bmi_value > 0");
                table.HasCheckConstraint("CK_meal_plans_3", "diet_type IN ('vegan','lacto_ovo_vegetarian')");
                table.HasCheckConstraint("CK_meal_plans_4", "ISJSON(allergies_snapshot) = 1");
                table.HasCheckConstraint("CK_meal_plans_5", "ISJSON(available_ingredients_snapshot) = 1");
                table.HasCheckConstraint("CK_meal_plans_6", "generation_source IN ('ai','manual')");
                table.HasCheckConstraint("CK_meal_plans_7", "status IN ('pending','generating','completed','failed','saved','archived')");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_meal_plans");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired(true);
            entity.Property(x => x.StartDate).HasColumnName("start_date").HasColumnType("DATE").IsRequired(true);
            entity.Property(x => x.EndDate).HasColumnName("end_date").HasColumnType("DATE").IsRequired(true);
            entity.Property(x => x.HeightCm).HasColumnName("height_cm").HasColumnType("DECIMAL(5,2)").IsRequired(true);
            entity.Property(x => x.WeightKg).HasColumnName("weight_kg").HasColumnType("DECIMAL(6,2)").IsRequired(true);
            entity.Property(x => x.BmiValue).HasColumnName("bmi_value").HasColumnType("DECIMAL(6,2)").IsRequired(true);
            entity.Property(x => x.DietType).HasColumnName("diet_type").HasColumnType("NVARCHAR(30)").IsRequired(true).HasDefaultValueSql("'vegan'");
            entity.Property(x => x.AllergiesSnapshot).HasColumnName("allergies_snapshot").HasColumnType("NVARCHAR(MAX)").IsRequired(true).HasDefaultValueSql("N'[]'");
            entity.Property(x => x.AvailableIngredientsSnapshot).HasColumnName("available_ingredients_snapshot").HasColumnType("NVARCHAR(MAX)").IsRequired(true).HasDefaultValueSql("N'[]'");
            entity.Property(x => x.GenerationSource).HasColumnName("generation_source").HasColumnType("NVARCHAR(20)").IsRequired(true).HasDefaultValueSql("'ai'");
            entity.Property(x => x.ModelName).HasColumnName("model_name").HasColumnType("NVARCHAR(100)").IsRequired(false);
            entity.Property(x => x.Status).HasColumnName("status").HasColumnType("NVARCHAR(20)").IsRequired(true).HasDefaultValueSql("'pending'");
            entity.Property(x => x.ErrorMessage).HasColumnName("error_message").HasColumnType("NVARCHAR(2000)").IsRequired(false);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.UpdatedAt).HasColumnName("updated_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.SavedAt).HasColumnName("saved_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.UserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_meal_plans_1");
            entity.HasIndex(x => new { x.UserId, x.StartDate }).HasDatabaseName("IX_meal_plans_1");
        });
        modelBuilder.Entity<Meal>(entity =>
        {
            entity.ToTable("meals", table =>
            {
                table.HasCheckConstraint("CK_meals_1", "servings > 0");
                table.HasCheckConstraint("CK_meals_2", "total_calories >= 0");
                table.HasCheckConstraint("CK_meals_3", "diet_type IN ('vegan','lacto_ovo_vegetarian')");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_meals");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.OwnerUserId).HasColumnName("owner_user_id").HasColumnType("BIGINT").IsRequired(false);
            entity.Property(x => x.ComboName).HasColumnName("combo_name").HasColumnType("NVARCHAR(255)").IsRequired(true);
            entity.Property(x => x.Description).HasColumnName("description").HasColumnType("NVARCHAR(MAX)").IsRequired(false);
            entity.Property(x => x.Servings).HasColumnName("servings").HasColumnType("DECIMAL(6,2)").IsRequired(true).HasDefaultValueSql("1");
            entity.Property(x => x.TotalCalories).HasColumnName("total_calories").HasColumnType("DECIMAL(10,2)").IsRequired(false);
            entity.Property(x => x.DietType).HasColumnName("diet_type").HasColumnType("NVARCHAR(30)").IsRequired(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.OwnerUserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_meals_1");
        });
        modelBuilder.Entity<MealPlanSchedule>(entity =>
        {
            entity.ToTable("meal_plan_schedule", table =>
            {
                table.HasCheckConstraint("CK_meal_plan_schedule_1", "day_of_week BETWEEN 1 AND 7");
                table.HasCheckConstraint("CK_meal_plan_schedule_2", "meal_time IN ('breakfast','lunch','dinner')");
                table.HasCheckConstraint("CK_meal_plan_schedule_3", "portion_multiplier > 0");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_meal_plan_schedule");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.MealPlanId).HasColumnName("meal_plan_id").HasColumnType("BIGINT").IsRequired(true);
            entity.Property(x => x.MealId).HasColumnName("meal_id").HasColumnType("BIGINT").IsRequired(true);
            entity.Property(x => x.DayOfWeek).HasColumnName("day_of_week").HasColumnType("TINYINT").IsRequired(true);
            entity.Property(x => x.MealTime).HasColumnName("meal_time").HasColumnType("NVARCHAR(20)").IsRequired(true);
            entity.Property(x => x.PortionMultiplier).HasColumnName("portion_multiplier").HasColumnType("DECIMAL(6,2)").IsRequired(true).HasDefaultValueSql("1");
            entity.HasAlternateKey(x => new { x.MealPlanId, x.DayOfWeek, x.MealTime }).HasName("UQ_meal_plan_schedule_1");
            entity.HasOne<MealPlan>().WithMany().HasForeignKey(x => new { x.MealPlanId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_meal_plan_schedule_1");
            entity.HasOne<Meal>().WithMany().HasForeignKey(x => new { x.MealId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_meal_plan_schedule_2");
        });
        modelBuilder.Entity<MealIngredient>(entity =>
        {
            entity.ToTable("meal_ingredients", table =>
            {
                table.HasCheckConstraint("CK_meal_ingredients_1", "calculated_quantity > 0");
            });
            entity.HasKey(x => new { x.MealId, x.IngredientId }).HasName("PK_meal_ingredients");
            entity.Property(x => x.MealId).HasColumnName("meal_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.IngredientId).HasColumnName("ingredient_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.CalculatedQuantity).HasColumnName("calculated_quantity").HasColumnType("DECIMAL(12,3)").IsRequired(false);
            entity.Property(x => x.Unit).HasColumnName("unit").HasColumnType("NVARCHAR(20)").IsRequired(true);
            entity.HasOne<Meal>().WithMany().HasForeignKey(x => new { x.MealId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_meal_ingredients_1");
            entity.HasOne<Ingredient>().WithMany().HasForeignKey(x => new { x.IngredientId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_meal_ingredients_2");
        });
        modelBuilder.Entity<ChatSession>(entity =>
        {
            entity.ToTable("chat_sessions", table =>
            {
                table.HasCheckConstraint("CK_chat_sessions_1", "user_id IS NOT NULL OR guest_session_id IS NOT NULL");
                table.HasCheckConstraint("CK_chat_sessions_2", "(context_summary IS NULL AND summary_through_message_id IS NULL) OR (context_summary IS NOT NULL AND summary_through_message_id IS NOT NULL AND summary_through_message_id > 0)");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_chat_sessions");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired(false);
            entity.Property(x => x.GuestSessionId).HasColumnName("guest_session_id").HasColumnType("UNIQUEIDENTIFIER").IsRequired(false);
            entity.Property(x => x.ContextSummary).HasColumnName("context_summary").HasColumnType("NVARCHAR(MAX)").IsRequired(false);
            entity.Property(x => x.SummaryThroughMessageId).HasColumnName("summary_through_message_id").HasColumnType("BIGINT").IsRequired(false);
            entity.Property(x => x.StartedAt).HasColumnName("started_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.LastActivityAt).HasColumnName("last_activity_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.UserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_chat_sessions_1");
            entity.HasIndex(x => new { x.UserId, x.LastActivityAt }).HasDatabaseName("IX_chat_sessions_1");
            entity.HasIndex(x => new { x.GuestSessionId, x.LastActivityAt }).HasDatabaseName("IX_chat_sessions_2");
        });
        modelBuilder.Entity<ChatMessage>(entity =>
        {
            entity.ToTable("chat_messages", table =>
            {
                table.HasCheckConstraint("CK_chat_messages_1", "sender_type IN ('user','ai')");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_chat_messages");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.SessionId).HasColumnName("session_id").HasColumnType("BIGINT").IsRequired(true);
            entity.Property(x => x.SenderType).HasColumnName("sender_type").HasColumnType("NVARCHAR(20)").IsRequired(true);
            entity.Property(x => x.MessageText).HasColumnName("message_text").HasColumnType("NVARCHAR(MAX)").IsRequired(true);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<ChatSession>().WithMany().HasForeignKey(x => new { x.SessionId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_chat_messages_1");
            entity.HasIndex(x => new { x.SessionId, x.Id }).HasDatabaseName("IX_chat_messages_1");
        });
        modelBuilder.Entity<AiUsage>(entity =>
        {
            entity.ToTable("ai_usage", table =>
            {
                table.HasCheckConstraint("CK_ai_usage_1", "user_id IS NOT NULL OR guest_session_id IS NOT NULL");
                table.HasCheckConstraint("CK_ai_usage_2", "feature_type IN ('chatbot','meal_planner','video_summary','embedding','chat_summary','content_moderation')");
                table.HasCheckConstraint("CK_ai_usage_3", "status IN ('pending','completed','failed')");
                table.HasCheckConstraint("CK_ai_usage_4", "input_tokens >= 0");
                table.HasCheckConstraint("CK_ai_usage_5", "output_tokens >= 0");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_ai_usage");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.RequestId).HasColumnName("request_id").HasColumnType("UNIQUEIDENTIFIER").IsRequired(true).HasDefaultValueSql("NEWID()");
            entity.HasAlternateKey(x => x.RequestId).HasName("UQ_ai_usage_request_id");
            entity.Property(x => x.UserId).HasColumnName("user_id").HasColumnType("BIGINT").IsRequired(false);
            entity.Property(x => x.GuestSessionId).HasColumnName("guest_session_id").HasColumnType("UNIQUEIDENTIFIER").IsRequired(false);
            entity.Property(x => x.FeatureType).HasColumnName("feature_type").HasColumnType("NVARCHAR(30)").IsRequired(true);
            entity.Property(x => x.Provider).HasColumnName("provider").HasColumnType("NVARCHAR(50)").IsRequired(true);
            entity.Property(x => x.ModelName).HasColumnName("model_name").HasColumnType("NVARCHAR(100)").IsRequired(true);
            entity.Property(x => x.Status).HasColumnName("status").HasColumnType("NVARCHAR(20)").IsRequired(true).HasDefaultValueSql("'pending'");
            entity.Property(x => x.InputTokens).HasColumnName("input_tokens").HasColumnType("INT").IsRequired(false);
            entity.Property(x => x.OutputTokens).HasColumnName("output_tokens").HasColumnType("INT").IsRequired(false);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.UserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_ai_usage_1");
            entity.HasIndex(x => new { x.UserId, x.FeatureType, x.CreatedAt }).HasDatabaseName("IX_ai_usage_1");
            entity.HasIndex(x => new { x.GuestSessionId, x.FeatureType, x.CreatedAt }).HasDatabaseName("IX_ai_usage_2");
        });
        modelBuilder.Entity<PostEmbedding>(entity =>
        {
            entity.ToTable("post_embeddings", table =>
            {
                table.HasCheckConstraint("CK_post_embeddings_1", "chunk_index >= 0");
                table.HasCheckConstraint("CK_post_embeddings_2", "status IN ('pending','processing','completed','failed','stale')");
                table.HasCheckConstraint("CK_post_embeddings_3", "status <> 'completed' OR (vector_id IS NOT NULL AND embedded_at IS NOT NULL)");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_post_embeddings");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.PostId).HasColumnName("post_id").HasColumnType("BIGINT").IsRequired(true);
            entity.Property(x => x.ChunkIndex).HasColumnName("chunk_index").HasColumnType("INT").IsRequired(true);
            entity.Property(x => x.VectorId).HasColumnName("vector_id").HasColumnType("NVARCHAR(255)").IsRequired(false);
            entity.Property(x => x.ContentHash).HasColumnName("content_hash").HasColumnType("VARCHAR(64)").IsRequired(true);
            entity.Property(x => x.EmbeddingModel).HasColumnName("embedding_model").HasColumnType("NVARCHAR(100)").IsRequired(true);
            entity.Property(x => x.Status).HasColumnName("status").HasColumnType("NVARCHAR(20)").IsRequired(true).HasDefaultValueSql("'pending'");
            entity.Property(x => x.EmbeddedAt).HasColumnName("embedded_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.HasAlternateKey(x => new { x.PostId, x.ChunkIndex }).HasName("UQ_post_embeddings_1");
            entity.HasOne<Post>().WithMany().HasForeignKey(x => new { x.PostId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_post_embeddings_1");
            entity.HasIndex(x => new { x.Status, x.PostId }).HasDatabaseName("IX_post_embeddings_1");
        });
        modelBuilder.Entity<Shop>(entity =>
        {
            entity.ToTable("shops", table =>
            {
                table.HasCheckConstraint("CK_shops_1", "latitude BETWEEN -90 AND 90");
                table.HasCheckConstraint("CK_shops_2", "longitude BETWEEN -180 AND 180");
                table.HasCheckConstraint("CK_shops_3", "(latitude IS NULL AND longitude IS NULL) OR (latitude IS NOT NULL AND longitude IS NOT NULL)");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_shops");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.SuggestedByUserId).HasColumnName("suggested_by_user_id").HasColumnType("BIGINT").IsRequired(false);
            entity.Property(x => x.GooglePlaceId).HasColumnName("google_place_id").HasColumnType("NVARCHAR(255)").IsRequired(false);
            entity.Property(x => x.Name).HasColumnName("name").HasColumnType("NVARCHAR(255)").IsRequired(true);
            entity.Property(x => x.Address).HasColumnName("address").HasColumnType("NVARCHAR(1000)").IsRequired(false);
            entity.Property(x => x.Latitude).HasColumnName("latitude").HasColumnType("DECIMAL(10,7)").IsRequired(false);
            entity.Property(x => x.Longitude).HasColumnName("longitude").HasColumnType("DECIMAL(10,7)").IsRequired(false);
            entity.Property(x => x.IsApproved).HasColumnName("is_approved").HasColumnType("BIT").IsRequired(true).HasDefaultValueSql("0");
            entity.Property(x => x.IsDeleted).HasColumnName("is_deleted").HasColumnType("BIT").IsRequired(true).HasDefaultValueSql("0");
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.SuggestedByUserId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_shops_1");
            entity.HasIndex(x => new { x.GooglePlaceId }).HasDatabaseName("IX_shops_1").IsUnique().HasFilter("google_place_id IS NOT NULL");
        });
        modelBuilder.Entity<ShopCategory>(entity =>
        {
            entity.ToTable("shop_categories", table =>
            {
            });
            entity.HasKey(x => new { x.ShopId, x.CategoryId }).HasName("PK_shop_categories");
            entity.Property(x => x.ShopId).HasColumnName("shop_id").HasColumnType("BIGINT").IsRequired(true).ValueGeneratedNever();
            entity.Property(x => x.CategoryId).HasColumnName("category_id").HasColumnType("INT").IsRequired(true).ValueGeneratedNever();
            entity.HasOne<Shop>().WithMany().HasForeignKey(x => new { x.ShopId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_shop_categories_1");
            entity.HasOne<Category>().WithMany().HasForeignKey(x => new { x.CategoryId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_shop_categories_2");
        });
        modelBuilder.Entity<Flag>(entity =>
        {
            entity.ToTable("flags", table =>
            {
                table.HasCheckConstraint("CK_flags_1", "status IN ('pending','resolved','dismissed')");
                table.HasCheckConstraint("CK_flags_2", "(CASE WHEN post_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN comment_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN shop_id IS NOT NULL THEN 1 ELSE 0 END) = 1");
                table.HasCheckConstraint("CK_flags_3", "(status = 'pending' AND resolved_by_admin_id IS NULL AND resolved_at IS NULL) OR (status IN ('resolved','dismissed') AND resolved_by_admin_id IS NOT NULL AND resolved_at IS NOT NULL)");
                table.HasCheckConstraint("CK_flags_4", "(source_type = 'user' AND reporter_id IS NOT NULL AND ai_model_name IS NULL) OR (source_type = 'ai' AND reporter_id IS NULL AND ai_model_name IS NOT NULL AND post_id IS NOT NULL)");
            });
            entity.HasKey(x => new { x.Id }).HasName("PK_flags");
            entity.Property(x => x.Id).HasColumnName("id").HasColumnType("BIGINT").IsRequired(true).UseIdentityColumn();
            entity.Property(x => x.ReporterId).HasColumnName("reporter_id").HasColumnType("BIGINT").IsRequired(false);
            entity.Property(x => x.PostId).HasColumnName("post_id").HasColumnType("BIGINT").IsRequired(false);
            entity.Property(x => x.CommentId).HasColumnName("comment_id").HasColumnType("BIGINT").IsRequired(false);
            entity.Property(x => x.ShopId).HasColumnName("shop_id").HasColumnType("BIGINT").IsRequired(false);
            entity.Property(x => x.Reason).HasColumnName("reason").HasColumnType("NVARCHAR(500)").IsRequired(true);
            entity.Property(x => x.Status).HasColumnName("status").HasColumnType("NVARCHAR(20)").IsRequired(true).HasDefaultValueSql("'pending'");
            entity.Property(x => x.ResolvedByAdminId).HasColumnName("resolved_by_admin_id").HasColumnType("BIGINT").IsRequired(false);
            entity.Property(x => x.ResolutionNote).HasColumnName("resolution_note").HasColumnType("NVARCHAR(2000)").IsRequired(false);
            entity.Property(x => x.CreatedAt).HasColumnName("created_at").HasColumnType("DATETIME2").IsRequired(true).HasDefaultValueSql("SYSUTCDATETIME()");
            entity.Property(x => x.ResolvedAt).HasColumnName("resolved_at").HasColumnType("DATETIME2").IsRequired(false);
            entity.Property(x => x.SourceType).HasColumnName("source_type").HasColumnType("NVARCHAR(10)").IsRequired(true).HasDefaultValueSql("'user'");
            entity.Property(x => x.AiModelName).HasColumnName("ai_model_name").HasColumnType("NVARCHAR(100)").IsRequired(false);
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.ReporterId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_flags_1");
            entity.HasOne<Post>().WithMany().HasForeignKey(x => new { x.PostId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_flags_2");
            entity.HasOne<Comment>().WithMany().HasForeignKey(x => new { x.CommentId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_flags_3");
            entity.HasOne<Shop>().WithMany().HasForeignKey(x => new { x.ShopId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_flags_4");
            entity.HasOne<User>().WithMany().HasForeignKey(x => new { x.ResolvedByAdminId }).HasPrincipalKey(x => new { x.Id }).OnDelete(DeleteBehavior.NoAction).HasConstraintName("FK_flags_5");
            entity.HasIndex(x => new { x.Status, x.CreatedAt }).HasDatabaseName("IX_flags_1");
        });
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        foreach (var property in entityType.GetProperties())
        {
            if (property.ClrType != typeof(bool)) continue;
            var defaultSql = property.GetDefaultValueSql();
            if (defaultSql is not ("0" or "1")) continue;
            property.SetDefaultValueSql(null);
            property.SetDefaultValue(defaultSql == "1");
        }
        modelBuilder.Entity<Role>().HasData(new Role { Id = 1, RoleName = "member" }, new Role { Id = 2, RoleName = "admin" });
    }
}
