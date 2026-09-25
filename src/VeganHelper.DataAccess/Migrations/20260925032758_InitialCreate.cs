using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace VeganHelper.DataAccess.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "categories",
                columns: table => new
                {
                    id = table.Column<int>(type: "INT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "NVARCHAR(100)", nullable: false),
                    slug = table.Column<string>(type: "NVARCHAR(120)", nullable: false),
                    category_type = table.Column<string>(type: "NVARCHAR(20)", nullable: false),
                    post_category_kind = table.Column<string>(type: "NVARCHAR(20)", nullable: true),
                    is_active = table.Column<bool>(type: "BIT", nullable: false, defaultValue: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_categories", x => x.id);
                    table.UniqueConstraint("UQ_categories_1", x => new { x.category_type, x.name });
                    table.UniqueConstraint("UQ_categories_slug", x => x.slug);
                    table.CheckConstraint("CK_categories_1", "category_type IN ('post','shop')");
                    table.CheckConstraint("CK_categories_2", "(category_type = 'shop' AND post_category_kind IS NULL) OR (category_type = 'post' AND post_category_kind IS NOT NULL AND post_category_kind IN ('food','recipe','topic'))");
                });

            migrationBuilder.CreateTable(
                name: "ingredients",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    name = table.Column<string>(type: "NVARCHAR(100)", nullable: false),
                    default_unit = table.Column<string>(type: "NVARCHAR(20)", nullable: false),
                    calories_per_100g = table.Column<decimal>(type: "DECIMAL(10,2)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ingredients", x => x.id);
                    table.UniqueConstraint("UQ_ingredients_name", x => x.name);
                    table.CheckConstraint("CK_ingredients_1", "calories_per_100g >= 0");
                });

            migrationBuilder.CreateTable(
                name: "roles",
                columns: table => new
                {
                    id = table.Column<int>(type: "INT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    role_name = table.Column<string>(type: "NVARCHAR(50)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_roles", x => x.id);
                    table.UniqueConstraint("UQ_roles_role_name", x => x.role_name);
                    table.CheckConstraint("CK_roles_1", "role_name IN ('member','admin')");
                });

            migrationBuilder.CreateTable(
                name: "users",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    username = table.Column<string>(type: "NVARCHAR(100)", nullable: false),
                    email = table.Column<string>(type: "NVARCHAR(255)", nullable: false),
                    password_hash = table.Column<string>(type: "NVARCHAR(500)", nullable: true),
                    email_verified_at = table.Column<DateTime>(type: "DATETIME2", nullable: true),
                    is_active = table.Column<bool>(type: "BIT", nullable: false, defaultValue: true),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "DATETIME2", nullable: true),
                    last_login_at = table.Column<DateTime>(type: "DATETIME2", nullable: true),
                    deleted_at = table.Column<DateTime>(type: "DATETIME2", nullable: true),
                    role_id = table.Column<int>(type: "INT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_users", x => x.id);
                    table.UniqueConstraint("UQ_users_email", x => x.email);
                    table.UniqueConstraint("UQ_users_username", x => x.username);
                    table.CheckConstraint("CK_users_1", "deleted_at IS NULL OR is_active = 0");
                    table.ForeignKey(
                        name: "FK_users_1",
                        column: x => x.role_id,
                        principalTable: "roles",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "ai_usage",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    request_id = table.Column<Guid>(type: "UNIQUEIDENTIFIER", nullable: false, defaultValueSql: "NEWID()"),
                    user_id = table.Column<long>(type: "BIGINT", nullable: true),
                    guest_session_id = table.Column<Guid>(type: "UNIQUEIDENTIFIER", nullable: true),
                    feature_type = table.Column<string>(type: "NVARCHAR(30)", nullable: false),
                    provider = table.Column<string>(type: "NVARCHAR(50)", nullable: false),
                    model_name = table.Column<string>(type: "NVARCHAR(100)", nullable: false),
                    status = table.Column<string>(type: "NVARCHAR(20)", nullable: false, defaultValueSql: "'pending'"),
                    input_tokens = table.Column<int>(type: "INT", nullable: true),
                    output_tokens = table.Column<int>(type: "INT", nullable: true),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ai_usage", x => x.id);
                    table.UniqueConstraint("UQ_ai_usage_request_id", x => x.request_id);
                    table.CheckConstraint("CK_ai_usage_1", "user_id IS NOT NULL OR guest_session_id IS NOT NULL");
                    table.CheckConstraint("CK_ai_usage_2", "feature_type IN ('chatbot','meal_planner','video_summary','embedding','chat_summary','content_moderation')");
                    table.CheckConstraint("CK_ai_usage_3", "status IN ('pending','completed','failed')");
                    table.CheckConstraint("CK_ai_usage_4", "input_tokens >= 0");
                    table.CheckConstraint("CK_ai_usage_5", "output_tokens >= 0");
                    table.ForeignKey(
                        name: "FK_ai_usage_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "chat_sessions",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "BIGINT", nullable: true),
                    guest_session_id = table.Column<Guid>(type: "UNIQUEIDENTIFIER", nullable: true),
                    context_summary = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    summary_through_message_id = table.Column<long>(type: "BIGINT", nullable: true),
                    started_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    last_activity_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_sessions", x => x.id);
                    table.CheckConstraint("CK_chat_sessions_1", "user_id IS NOT NULL OR guest_session_id IS NOT NULL");
                    table.CheckConstraint("CK_chat_sessions_2", "(context_summary IS NULL AND summary_through_message_id IS NULL) OR (context_summary IS NOT NULL AND summary_through_message_id IS NOT NULL AND summary_through_message_id > 0)");
                    table.ForeignKey(
                        name: "FK_chat_sessions_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "meal_plans",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "BIGINT", nullable: false),
                    start_date = table.Column<DateOnly>(type: "DATE", nullable: false),
                    end_date = table.Column<DateOnly>(type: "DATE", nullable: false),
                    height_cm = table.Column<decimal>(type: "DECIMAL(5,2)", nullable: false),
                    weight_kg = table.Column<decimal>(type: "DECIMAL(6,2)", nullable: false),
                    bmi_value = table.Column<decimal>(type: "DECIMAL(6,2)", nullable: false),
                    diet_type = table.Column<string>(type: "NVARCHAR(30)", nullable: false, defaultValueSql: "'vegan'"),
                    allergies_snapshot = table.Column<string>(type: "NVARCHAR(MAX)", nullable: false, defaultValueSql: "N'[]'"),
                    available_ingredients_snapshot = table.Column<string>(type: "NVARCHAR(MAX)", nullable: false, defaultValueSql: "N'[]'"),
                    generation_source = table.Column<string>(type: "NVARCHAR(20)", nullable: false, defaultValueSql: "'ai'"),
                    model_name = table.Column<string>(type: "NVARCHAR(100)", nullable: true),
                    status = table.Column<string>(type: "NVARCHAR(20)", nullable: false, defaultValueSql: "'pending'"),
                    error_message = table.Column<string>(type: "NVARCHAR(2000)", nullable: true),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "DATETIME2", nullable: true),
                    saved_at = table.Column<DateTime>(type: "DATETIME2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plans", x => x.id);
                    table.CheckConstraint("CK_meal_plans_1", "DATEDIFF(DAY, start_date, end_date) = 6");
                    table.CheckConstraint("CK_meal_plans_2", "height_cm > 0 AND weight_kg > 0 AND bmi_value > 0");
                    table.CheckConstraint("CK_meal_plans_3", "diet_type IN ('vegan','lacto_ovo_vegetarian')");
                    table.CheckConstraint("CK_meal_plans_4", "ISJSON(allergies_snapshot) = 1");
                    table.CheckConstraint("CK_meal_plans_5", "ISJSON(available_ingredients_snapshot) = 1");
                    table.CheckConstraint("CK_meal_plans_6", "generation_source IN ('ai','manual')");
                    table.CheckConstraint("CK_meal_plans_7", "status IN ('pending','generating','completed','failed','saved','archived')");
                    table.ForeignKey(
                        name: "FK_meal_plans_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "meals",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    owner_user_id = table.Column<long>(type: "BIGINT", nullable: true),
                    combo_name = table.Column<string>(type: "NVARCHAR(255)", nullable: false),
                    description = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    servings = table.Column<decimal>(type: "DECIMAL(6,2)", nullable: false, defaultValueSql: "1"),
                    total_calories = table.Column<decimal>(type: "DECIMAL(10,2)", nullable: true),
                    diet_type = table.Column<string>(type: "NVARCHAR(30)", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meals", x => x.id);
                    table.CheckConstraint("CK_meals_1", "servings > 0");
                    table.CheckConstraint("CK_meals_2", "total_calories >= 0");
                    table.CheckConstraint("CK_meals_3", "diet_type IN ('vegan','lacto_ovo_vegetarian')");
                    table.ForeignKey(
                        name: "FK_meals_1",
                        column: x => x.owner_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "posts",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    author_id = table.Column<long>(type: "BIGINT", nullable: false),
                    post_type = table.Column<string>(type: "NVARCHAR(20)", nullable: false),
                    title = table.Column<string>(type: "NVARCHAR(255)", nullable: false),
                    content = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    meal_type = table.Column<string>(type: "NVARCHAR(20)", nullable: true),
                    prep_time_mins = table.Column<int>(type: "INT", nullable: true),
                    cooking_time_mins = table.Column<int>(type: "INT", nullable: true),
                    servings = table.Column<int>(type: "INT", nullable: true),
                    calories_per_serving = table.Column<decimal>(type: "DECIMAL(10,2)", nullable: true),
                    diet_type = table.Column<string>(type: "NVARCHAR(30)", nullable: true),
                    ingredients_verified = table.Column<bool>(type: "BIT", nullable: false, defaultValue: false),
                    status = table.Column<string>(type: "NVARCHAR(20)", nullable: false, defaultValueSql: "'draft'"),
                    view_count = table.Column<long>(type: "BIGINT", nullable: false, defaultValueSql: "0"),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "DATETIME2", nullable: true),
                    is_deleted = table.Column<bool>(type: "BIT", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "DATETIME2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_posts", x => x.id);
                    table.CheckConstraint("CK_posts_1", "post_type IN ('article','video','recipe','community')");
                    table.CheckConstraint("CK_posts_10", "(is_deleted = 0 AND deleted_at IS NULL) OR (is_deleted = 1 AND deleted_at IS NOT NULL)");
                    table.CheckConstraint("CK_posts_2", "meal_type IN ('breakfast','lunch','dinner','snack')");
                    table.CheckConstraint("CK_posts_3", "diet_type IN ('vegan','lacto_ovo_vegetarian')");
                    table.CheckConstraint("CK_posts_4", "status IN ('draft','pending_review','published','rejected','hidden')");
                    table.CheckConstraint("CK_posts_5", "prep_time_mins >= 0");
                    table.CheckConstraint("CK_posts_6", "cooking_time_mins >= 0");
                    table.CheckConstraint("CK_posts_7", "servings > 0");
                    table.CheckConstraint("CK_posts_8", "calories_per_serving >= 0");
                    table.CheckConstraint("CK_posts_9", "view_count >= 0");
                    table.ForeignKey(
                        name: "FK_posts_1",
                        column: x => x.author_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "shops",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    suggested_by_user_id = table.Column<long>(type: "BIGINT", nullable: true),
                    google_place_id = table.Column<string>(type: "NVARCHAR(255)", nullable: true),
                    name = table.Column<string>(type: "NVARCHAR(255)", nullable: false),
                    address = table.Column<string>(type: "NVARCHAR(1000)", nullable: true),
                    latitude = table.Column<decimal>(type: "DECIMAL(10,7)", nullable: true),
                    longitude = table.Column<decimal>(type: "DECIMAL(10,7)", nullable: true),
                    is_approved = table.Column<bool>(type: "BIT", nullable: false, defaultValue: false),
                    is_deleted = table.Column<bool>(type: "BIT", nullable: false, defaultValue: false),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shops", x => x.id);
                    table.CheckConstraint("CK_shops_1", "latitude BETWEEN -90 AND 90");
                    table.CheckConstraint("CK_shops_2", "longitude BETWEEN -180 AND 180");
                    table.CheckConstraint("CK_shops_3", "(latitude IS NULL AND longitude IS NULL) OR (latitude IS NOT NULL AND longitude IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_shops_1",
                        column: x => x.suggested_by_user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_allergies",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "BIGINT", nullable: false),
                    ingredient_id = table.Column<long>(type: "BIGINT", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_allergies", x => new { x.user_id, x.ingredient_id });
                    table.ForeignKey(
                        name: "FK_user_allergies_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_allergies_2",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_available_ingredients",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "BIGINT", nullable: false),
                    ingredient_id = table.Column<long>(type: "BIGINT", nullable: false),
                    quantity = table.Column<decimal>(type: "DECIMAL(12,3)", nullable: true),
                    unit = table.Column<string>(type: "NVARCHAR(20)", nullable: false),
                    updated_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_available_ingredients", x => new { x.user_id, x.ingredient_id });
                    table.CheckConstraint("CK_user_available_ingredients_1", "quantity >= 0");
                    table.ForeignKey(
                        name: "FK_user_available_ingredients_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_user_available_ingredients_2",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_identities",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    user_id = table.Column<long>(type: "BIGINT", nullable: false),
                    provider = table.Column<string>(type: "NVARCHAR(20)", nullable: false, defaultValueSql: "'google'"),
                    provider_subject = table.Column<string>(type: "NVARCHAR(255)", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_identities", x => x.id);
                    table.UniqueConstraint("UQ_user_identities_1", x => new { x.provider, x.provider_subject });
                    table.CheckConstraint("CK_user_identities_1", "provider IN ('google')");
                    table.ForeignKey(
                        name: "FK_user_identities_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "user_profiles",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "BIGINT", nullable: false),
                    display_name = table.Column<string>(type: "NVARCHAR(100)", nullable: false),
                    avatar_url = table.Column<string>(type: "NVARCHAR(1000)", nullable: true),
                    height_cm = table.Column<decimal>(type: "DECIMAL(5,2)", nullable: true),
                    weight_kg = table.Column<decimal>(type: "DECIMAL(6,2)", nullable: true),
                    birth_date = table.Column<DateOnly>(type: "DATE", nullable: true),
                    biological_sex = table.Column<string>(type: "NVARCHAR(20)", nullable: true),
                    diet_type = table.Column<string>(type: "NVARCHAR(30)", nullable: false, defaultValueSql: "'vegan'"),
                    updated_at = table.Column<DateTime>(type: "DATETIME2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profiles", x => x.user_id);
                    table.CheckConstraint("CK_user_profiles_1", "height_cm IS NULL OR height_cm > 0");
                    table.CheckConstraint("CK_user_profiles_2", "weight_kg IS NULL OR weight_kg > 0");
                    table.CheckConstraint("CK_user_profiles_3", "biological_sex IN ('male','female','other')");
                    table.CheckConstraint("CK_user_profiles_4", "diet_type IN ('vegan','lacto_ovo_vegetarian')");
                    table.ForeignKey(
                        name: "FK_user_profiles_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "chat_messages",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    session_id = table.Column<long>(type: "BIGINT", nullable: false),
                    sender_type = table.Column<string>(type: "NVARCHAR(20)", nullable: false),
                    message_text = table.Column<string>(type: "NVARCHAR(MAX)", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_chat_messages", x => x.id);
                    table.CheckConstraint("CK_chat_messages_1", "sender_type IN ('user','ai')");
                    table.ForeignKey(
                        name: "FK_chat_messages_1",
                        column: x => x.session_id,
                        principalTable: "chat_sessions",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "meal_ingredients",
                columns: table => new
                {
                    meal_id = table.Column<long>(type: "BIGINT", nullable: false),
                    ingredient_id = table.Column<long>(type: "BIGINT", nullable: false),
                    calculated_quantity = table.Column<decimal>(type: "DECIMAL(12,3)", nullable: true),
                    unit = table.Column<string>(type: "NVARCHAR(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_ingredients", x => new { x.meal_id, x.ingredient_id });
                    table.CheckConstraint("CK_meal_ingredients_1", "calculated_quantity > 0");
                    table.ForeignKey(
                        name: "FK_meal_ingredients_1",
                        column: x => x.meal_id,
                        principalTable: "meals",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_meal_ingredients_2",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "meal_plan_schedule",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    meal_plan_id = table.Column<long>(type: "BIGINT", nullable: false),
                    meal_id = table.Column<long>(type: "BIGINT", nullable: false),
                    day_of_week = table.Column<byte>(type: "TINYINT", nullable: false),
                    meal_time = table.Column<string>(type: "NVARCHAR(20)", nullable: false),
                    portion_multiplier = table.Column<decimal>(type: "DECIMAL(6,2)", nullable: false, defaultValueSql: "1")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_meal_plan_schedule", x => x.id);
                    table.UniqueConstraint("UQ_meal_plan_schedule_1", x => new { x.meal_plan_id, x.day_of_week, x.meal_time });
                    table.CheckConstraint("CK_meal_plan_schedule_1", "day_of_week BETWEEN 1 AND 7");
                    table.CheckConstraint("CK_meal_plan_schedule_2", "meal_time IN ('breakfast','lunch','dinner')");
                    table.CheckConstraint("CK_meal_plan_schedule_3", "portion_multiplier > 0");
                    table.ForeignKey(
                        name: "FK_meal_plan_schedule_1",
                        column: x => x.meal_plan_id,
                        principalTable: "meal_plans",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_meal_plan_schedule_2",
                        column: x => x.meal_id,
                        principalTable: "meals",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "comments",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    post_id = table.Column<long>(type: "BIGINT", nullable: false),
                    user_id = table.Column<long>(type: "BIGINT", nullable: false),
                    parent_comment_id = table.Column<long>(type: "BIGINT", nullable: true),
                    content = table.Column<string>(type: "NVARCHAR(1000)", nullable: false),
                    status = table.Column<string>(type: "NVARCHAR(20)", nullable: false, defaultValueSql: "'visible'"),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    updated_at = table.Column<DateTime>(type: "DATETIME2", nullable: true),
                    is_deleted = table.Column<bool>(type: "BIT", nullable: false, defaultValue: false),
                    deleted_at = table.Column<DateTime>(type: "DATETIME2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_comments", x => x.id);
                    table.UniqueConstraint("UQ_comments_1", x => new { x.post_id, x.id });
                    table.CheckConstraint("CK_comments_1", "parent_comment_id IS NULL OR parent_comment_id <> id");
                    table.CheckConstraint("CK_comments_2", "status IN ('visible','hidden')");
                    table.CheckConstraint("CK_comments_3", "(is_deleted = 0 AND deleted_at IS NULL) OR (is_deleted = 1 AND deleted_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_comments_1",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_comments_2",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_comments_3",
                        columns: x => new { x.post_id, x.parent_comment_id },
                        principalTable: "comments",
                        principalColumns: new[] { "post_id", "id" });
                });

            migrationBuilder.CreateTable(
                name: "post_categories",
                columns: table => new
                {
                    post_id = table.Column<long>(type: "BIGINT", nullable: false),
                    category_id = table.Column<int>(type: "INT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_categories", x => new { x.post_id, x.category_id });
                    table.ForeignKey(
                        name: "FK_post_categories_1",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_post_categories_2",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "post_embeddings",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    post_id = table.Column<long>(type: "BIGINT", nullable: false),
                    chunk_index = table.Column<int>(type: "INT", nullable: false),
                    vector_id = table.Column<string>(type: "NVARCHAR(255)", nullable: true),
                    content_hash = table.Column<string>(type: "VARCHAR(64)", nullable: false),
                    embedding_model = table.Column<string>(type: "NVARCHAR(100)", nullable: false),
                    status = table.Column<string>(type: "NVARCHAR(20)", nullable: false, defaultValueSql: "'pending'"),
                    embedded_at = table.Column<DateTime>(type: "DATETIME2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_embeddings", x => x.id);
                    table.UniqueConstraint("UQ_post_embeddings_1", x => new { x.post_id, x.chunk_index });
                    table.CheckConstraint("CK_post_embeddings_1", "chunk_index >= 0");
                    table.CheckConstraint("CK_post_embeddings_2", "status IN ('pending','processing','completed','failed','stale')");
                    table.CheckConstraint("CK_post_embeddings_3", "status <> 'completed' OR (vector_id IS NOT NULL AND embedded_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_post_embeddings_1",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "post_ingredients",
                columns: table => new
                {
                    post_id = table.Column<long>(type: "BIGINT", nullable: false),
                    ingredient_id = table.Column<long>(type: "BIGINT", nullable: false),
                    quantity = table.Column<decimal>(type: "DECIMAL(12,3)", nullable: true),
                    unit = table.Column<string>(type: "NVARCHAR(20)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_ingredients", x => new { x.post_id, x.ingredient_id });
                    table.CheckConstraint("CK_post_ingredients_1", "quantity > 0");
                    table.ForeignKey(
                        name: "FK_post_ingredients_1",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_post_ingredients_2",
                        column: x => x.ingredient_id,
                        principalTable: "ingredients",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "post_likes",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "BIGINT", nullable: false),
                    post_id = table.Column<long>(type: "BIGINT", nullable: false),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_likes", x => new { x.user_id, x.post_id });
                    table.ForeignKey(
                        name: "FK_post_likes_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_post_likes_2",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "post_media",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    post_id = table.Column<long>(type: "BIGINT", nullable: false),
                    media_url = table.Column<string>(type: "NVARCHAR(1000)", nullable: false),
                    media_type = table.Column<string>(type: "NVARCHAR(20)", nullable: false),
                    cloud_public_id = table.Column<string>(type: "NVARCHAR(255)", nullable: true),
                    thumbnail_url = table.Column<string>(type: "NVARCHAR(1000)", nullable: true),
                    duration_seconds = table.Column<int>(type: "INT", nullable: true),
                    is_primary = table.Column<bool>(type: "BIT", nullable: false, defaultValue: false),
                    display_order = table.Column<int>(type: "INT", nullable: false, defaultValueSql: "0"),
                    processing_status = table.Column<string>(type: "NVARCHAR(20)", nullable: false, defaultValueSql: "'ready'"),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_media", x => x.id);
                    table.UniqueConstraint("UQ_post_media_1", x => new { x.post_id, x.id });
                    table.CheckConstraint("CK_post_media_1", "media_type IN ('image','video')");
                    table.CheckConstraint("CK_post_media_2", "processing_status IN ('uploading','processing','ready','failed')");
                    table.CheckConstraint("CK_post_media_3", "duration_seconds >= 0");
                    table.CheckConstraint("CK_post_media_4", "display_order >= 0");
                    table.ForeignKey(
                        name: "FK_post_media_1",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "saved_posts",
                columns: table => new
                {
                    user_id = table.Column<long>(type: "BIGINT", nullable: false),
                    post_id = table.Column<long>(type: "BIGINT", nullable: false),
                    saved_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()")
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_saved_posts", x => new { x.user_id, x.post_id });
                    table.ForeignKey(
                        name: "FK_saved_posts_1",
                        column: x => x.user_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_saved_posts_2",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "shop_categories",
                columns: table => new
                {
                    shop_id = table.Column<long>(type: "BIGINT", nullable: false),
                    category_id = table.Column<int>(type: "INT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_shop_categories", x => new { x.shop_id, x.category_id });
                    table.ForeignKey(
                        name: "FK_shop_categories_1",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_shop_categories_2",
                        column: x => x.category_id,
                        principalTable: "categories",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "flags",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    reporter_id = table.Column<long>(type: "BIGINT", nullable: true),
                    post_id = table.Column<long>(type: "BIGINT", nullable: true),
                    comment_id = table.Column<long>(type: "BIGINT", nullable: true),
                    shop_id = table.Column<long>(type: "BIGINT", nullable: true),
                    reason = table.Column<string>(type: "NVARCHAR(500)", nullable: false),
                    status = table.Column<string>(type: "NVARCHAR(20)", nullable: false, defaultValueSql: "'pending'"),
                    resolved_by_admin_id = table.Column<long>(type: "BIGINT", nullable: true),
                    resolution_note = table.Column<string>(type: "NVARCHAR(2000)", nullable: true),
                    created_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    resolved_at = table.Column<DateTime>(type: "DATETIME2", nullable: true),
                    source_type = table.Column<string>(type: "NVARCHAR(10)", nullable: false, defaultValueSql: "'user'"),
                    ai_model_name = table.Column<string>(type: "NVARCHAR(100)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_flags", x => x.id);
                    table.CheckConstraint("CK_flags_1", "status IN ('pending','resolved','dismissed')");
                    table.CheckConstraint("CK_flags_2", "(CASE WHEN post_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN comment_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN shop_id IS NOT NULL THEN 1 ELSE 0 END) = 1");
                    table.CheckConstraint("CK_flags_3", "(status = 'pending' AND resolved_by_admin_id IS NULL AND resolved_at IS NULL) OR (status IN ('resolved','dismissed') AND resolved_by_admin_id IS NOT NULL AND resolved_at IS NOT NULL)");
                    table.CheckConstraint("CK_flags_4", "(source_type = 'user' AND reporter_id IS NOT NULL AND ai_model_name IS NULL) OR (source_type = 'ai' AND reporter_id IS NULL AND ai_model_name IS NOT NULL AND post_id IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_flags_1",
                        column: x => x.reporter_id,
                        principalTable: "users",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_flags_2",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_flags_3",
                        column: x => x.comment_id,
                        principalTable: "comments",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_flags_4",
                        column: x => x.shop_id,
                        principalTable: "shops",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_flags_5",
                        column: x => x.resolved_by_admin_id,
                        principalTable: "users",
                        principalColumn: "id");
                });

            migrationBuilder.CreateTable(
                name: "post_summaries",
                columns: table => new
                {
                    post_id = table.Column<long>(type: "BIGINT", nullable: false),
                    source_media_id = table.Column<long>(type: "BIGINT", nullable: false),
                    transcript = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    ai_generated_text = table.Column<string>(type: "NVARCHAR(MAX)", nullable: true),
                    status = table.Column<string>(type: "NVARCHAR(20)", nullable: false, defaultValueSql: "'pending'"),
                    model_name = table.Column<string>(type: "NVARCHAR(100)", nullable: true),
                    error_message = table.Column<string>(type: "NVARCHAR(2000)", nullable: true),
                    requested_at = table.Column<DateTime>(type: "DATETIME2", nullable: false, defaultValueSql: "SYSUTCDATETIME()"),
                    generated_at = table.Column<DateTime>(type: "DATETIME2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_summaries", x => x.post_id);
                    table.CheckConstraint("CK_post_summaries_1", "status IN ('pending','processing','completed','failed','stale')");
                    table.CheckConstraint("CK_post_summaries_2", "status <> 'completed' OR (transcript IS NOT NULL AND ai_generated_text IS NOT NULL AND generated_at IS NOT NULL)");
                    table.ForeignKey(
                        name: "FK_post_summaries_1",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id");
                    table.ForeignKey(
                        name: "FK_post_summaries_2",
                        columns: x => new { x.post_id, x.source_media_id },
                        principalTable: "post_media",
                        principalColumns: new[] { "post_id", "id" });
                });

            migrationBuilder.InsertData(
                table: "roles",
                columns: new[] { "id", "role_name" },
                values: new object[,]
                {
                    { 1, "member" },
                    { 2, "admin" }
                });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_1",
                table: "ai_usage",
                columns: new[] { "user_id", "feature_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_ai_usage_2",
                table: "ai_usage",
                columns: new[] { "guest_session_id", "feature_type", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_messages_1",
                table: "chat_messages",
                columns: new[] { "session_id", "id" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_sessions_1",
                table: "chat_sessions",
                columns: new[] { "user_id", "last_activity_at" });

            migrationBuilder.CreateIndex(
                name: "IX_chat_sessions_2",
                table: "chat_sessions",
                columns: new[] { "guest_session_id", "last_activity_at" });

            migrationBuilder.CreateIndex(
                name: "IX_comments_1",
                table: "comments",
                columns: new[] { "post_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_comments_post_id_parent_comment_id",
                table: "comments",
                columns: new[] { "post_id", "parent_comment_id" });

            migrationBuilder.CreateIndex(
                name: "IX_comments_user_id",
                table: "comments",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_flags_1",
                table: "flags",
                columns: new[] { "status", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_flags_comment_id",
                table: "flags",
                column: "comment_id");

            migrationBuilder.CreateIndex(
                name: "IX_flags_post_id",
                table: "flags",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "IX_flags_reporter_id",
                table: "flags",
                column: "reporter_id");

            migrationBuilder.CreateIndex(
                name: "IX_flags_resolved_by_admin_id",
                table: "flags",
                column: "resolved_by_admin_id");

            migrationBuilder.CreateIndex(
                name: "IX_flags_shop_id",
                table: "flags",
                column: "shop_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_ingredients_ingredient_id",
                table: "meal_ingredients",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plan_schedule_meal_id",
                table: "meal_plan_schedule",
                column: "meal_id");

            migrationBuilder.CreateIndex(
                name: "IX_meal_plans_1",
                table: "meal_plans",
                columns: new[] { "user_id", "start_date" });

            migrationBuilder.CreateIndex(
                name: "IX_meals_owner_user_id",
                table: "meals",
                column: "owner_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_post_categories_category_id",
                table: "post_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_post_embeddings_1",
                table: "post_embeddings",
                columns: new[] { "status", "post_id" });

            migrationBuilder.CreateIndex(
                name: "IX_post_ingredients_ingredient_id",
                table: "post_ingredients",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_post_likes_post_id",
                table: "post_likes",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "IX_post_media_1",
                table: "post_media",
                column: "post_id",
                unique: true,
                filter: "is_primary = 1");

            migrationBuilder.CreateIndex(
                name: "IX_post_summaries_post_id_source_media_id",
                table: "post_summaries",
                columns: new[] { "post_id", "source_media_id" });

            migrationBuilder.CreateIndex(
                name: "IX_posts_1",
                table: "posts",
                columns: new[] { "status", "is_deleted", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_posts_2",
                table: "posts",
                columns: new[] { "author_id", "created_at" });

            migrationBuilder.CreateIndex(
                name: "IX_saved_posts_post_id",
                table: "saved_posts",
                column: "post_id");

            migrationBuilder.CreateIndex(
                name: "IX_shop_categories_category_id",
                table: "shop_categories",
                column: "category_id");

            migrationBuilder.CreateIndex(
                name: "IX_shops_1",
                table: "shops",
                column: "google_place_id",
                unique: true,
                filter: "google_place_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "IX_shops_suggested_by_user_id",
                table: "shops",
                column: "suggested_by_user_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_allergies_ingredient_id",
                table: "user_allergies",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_available_ingredients_ingredient_id",
                table: "user_available_ingredients",
                column: "ingredient_id");

            migrationBuilder.CreateIndex(
                name: "IX_user_identities_user_id",
                table: "user_identities",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "IX_users_role_id",
                table: "users",
                column: "role_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ai_usage");

            migrationBuilder.DropTable(
                name: "chat_messages");

            migrationBuilder.DropTable(
                name: "flags");

            migrationBuilder.DropTable(
                name: "meal_ingredients");

            migrationBuilder.DropTable(
                name: "meal_plan_schedule");

            migrationBuilder.DropTable(
                name: "post_categories");

            migrationBuilder.DropTable(
                name: "post_embeddings");

            migrationBuilder.DropTable(
                name: "post_ingredients");

            migrationBuilder.DropTable(
                name: "post_likes");

            migrationBuilder.DropTable(
                name: "post_summaries");

            migrationBuilder.DropTable(
                name: "saved_posts");

            migrationBuilder.DropTable(
                name: "shop_categories");

            migrationBuilder.DropTable(
                name: "user_allergies");

            migrationBuilder.DropTable(
                name: "user_available_ingredients");

            migrationBuilder.DropTable(
                name: "user_identities");

            migrationBuilder.DropTable(
                name: "user_profiles");

            migrationBuilder.DropTable(
                name: "chat_sessions");

            migrationBuilder.DropTable(
                name: "comments");

            migrationBuilder.DropTable(
                name: "meal_plans");

            migrationBuilder.DropTable(
                name: "meals");

            migrationBuilder.DropTable(
                name: "post_media");

            migrationBuilder.DropTable(
                name: "shops");

            migrationBuilder.DropTable(
                name: "categories");

            migrationBuilder.DropTable(
                name: "ingredients");

            migrationBuilder.DropTable(
                name: "posts");

            migrationBuilder.DropTable(
                name: "users");

            migrationBuilder.DropTable(
                name: "roles");
        }
    }
}
