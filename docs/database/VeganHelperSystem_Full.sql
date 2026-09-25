IF DB_ID(N'VeganHelperSystem') IS NULL
BEGIN
    CREATE DATABASE [VeganHelperSystem];
END;
GO
USE [VeganHelperSystem];
GO

CREATE TABLE [users] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [username] NVARCHAR(100) NOT NULL,
    [email] NVARCHAR(255) NOT NULL,
    [password_hash] NVARCHAR(500) NULL,
    [email_verified_at] DATETIME2 NULL,
    [is_active] BIT NOT NULL DEFAULT 1,
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [updated_at] DATETIME2 NULL,
    [last_login_at] DATETIME2 NULL,
    [deleted_at] DATETIME2 NULL,
    [role_id] INT NOT NULL,
    CONSTRAINT [PK_users] PRIMARY KEY ([id]),
    CONSTRAINT [UQ_users_username] UNIQUE ([username]),
    CONSTRAINT [UQ_users_email] UNIQUE ([email]),
    CONSTRAINT [CK_users_1] CHECK (deleted_at IS NULL OR is_active = 0)
);

CREATE TABLE [user_profiles] (
    [user_id] BIGINT NOT NULL,
    [display_name] NVARCHAR(100) NOT NULL,
    [avatar_url] NVARCHAR(1000) NULL,
    [height_cm] DECIMAL(5,2) NULL,
    [weight_kg] DECIMAL(6,2) NULL,
    [birth_date] DATE NULL,
    [biological_sex] NVARCHAR(20) NULL,
    [diet_type] NVARCHAR(30) NOT NULL DEFAULT 'vegan',
    [updated_at] DATETIME2 NULL,
    CONSTRAINT [PK_user_profiles] PRIMARY KEY ([user_id]),
    CONSTRAINT [CK_user_profiles_1] CHECK (height_cm IS NULL OR height_cm > 0),
    CONSTRAINT [CK_user_profiles_2] CHECK (weight_kg IS NULL OR weight_kg > 0),
    CONSTRAINT [CK_user_profiles_3] CHECK (biological_sex IN ('male','female','other')),
    CONSTRAINT [CK_user_profiles_4] CHECK (diet_type IN ('vegan','lacto_ovo_vegetarian'))
);

CREATE TABLE [roles] (
    [id] INT IDENTITY(1,1) NOT NULL,
    [role_name] NVARCHAR(50) NOT NULL,
    CONSTRAINT [PK_roles] PRIMARY KEY ([id]),
    CONSTRAINT [UQ_roles_role_name] UNIQUE ([role_name]),
    CONSTRAINT [CK_roles_1] CHECK (role_name IN ('member','admin'))
);

CREATE TABLE [user_identities] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [user_id] BIGINT NOT NULL,
    [provider] NVARCHAR(20) NOT NULL DEFAULT 'google',
    [provider_subject] NVARCHAR(255) NOT NULL,
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_user_identities] PRIMARY KEY ([id]),
    CONSTRAINT [UQ_user_identities_1] UNIQUE ([provider], [provider_subject]),
    CONSTRAINT [CK_user_identities_1] CHECK (provider IN ('google'))
);

CREATE TABLE [posts] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [author_id] BIGINT NOT NULL,
    [post_type] NVARCHAR(20) NOT NULL,
    [title] NVARCHAR(255) NOT NULL,
    [content] NVARCHAR(MAX) NULL,
    [meal_type] NVARCHAR(20) NULL,
    [prep_time_mins] INT NULL,
    [cooking_time_mins] INT NULL,
    [servings] INT NULL,
    [calories_per_serving] DECIMAL(10,2) NULL,
    [diet_type] NVARCHAR(30) NULL,
    [ingredients_verified] BIT NOT NULL DEFAULT 0,
    [status] NVARCHAR(20) NOT NULL DEFAULT 'draft',
    [view_count] BIGINT NOT NULL DEFAULT 0,
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [updated_at] DATETIME2 NULL,
    [is_deleted] BIT NOT NULL DEFAULT 0,
    [deleted_at] DATETIME2 NULL,
    CONSTRAINT [PK_posts] PRIMARY KEY ([id]),
    CONSTRAINT [CK_posts_1] CHECK (post_type IN ('article','video','recipe','community')),
    CONSTRAINT [CK_posts_2] CHECK (meal_type IN ('breakfast','lunch','dinner','snack')),
    CONSTRAINT [CK_posts_3] CHECK (diet_type IN ('vegan','lacto_ovo_vegetarian')),
    CONSTRAINT [CK_posts_4] CHECK (status IN ('draft','pending_review','published','rejected','hidden')),
    CONSTRAINT [CK_posts_5] CHECK (prep_time_mins >= 0),
    CONSTRAINT [CK_posts_6] CHECK (cooking_time_mins >= 0),
    CONSTRAINT [CK_posts_7] CHECK (servings > 0),
    CONSTRAINT [CK_posts_8] CHECK (calories_per_serving >= 0),
    CONSTRAINT [CK_posts_9] CHECK (view_count >= 0),
    CONSTRAINT [CK_posts_10] CHECK ((is_deleted = 0 AND deleted_at IS NULL) OR (is_deleted = 1 AND deleted_at IS NOT NULL))
);

CREATE TABLE [post_media] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [post_id] BIGINT NOT NULL,
    [media_url] NVARCHAR(1000) NOT NULL,
    [media_type] NVARCHAR(20) NOT NULL,
    [cloud_public_id] NVARCHAR(255) NULL,
    [thumbnail_url] NVARCHAR(1000) NULL,
    [duration_seconds] INT NULL,
    [is_primary] BIT NOT NULL DEFAULT 0,
    [display_order] INT NOT NULL DEFAULT 0,
    [processing_status] NVARCHAR(20) NOT NULL DEFAULT 'ready',
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_post_media] PRIMARY KEY ([id]),
    CONSTRAINT [UQ_post_media_1] UNIQUE ([post_id], [id]),
    CONSTRAINT [CK_post_media_1] CHECK (media_type IN ('image','video')),
    CONSTRAINT [CK_post_media_2] CHECK (processing_status IN ('uploading','processing','ready','failed')),
    CONSTRAINT [CK_post_media_3] CHECK (duration_seconds >= 0),
    CONSTRAINT [CK_post_media_4] CHECK (display_order >= 0)
);

CREATE TABLE [post_summaries] (
    [post_id] BIGINT NOT NULL,
    [source_media_id] BIGINT NOT NULL,
    [transcript] NVARCHAR(MAX) NULL,
    [ai_generated_text] NVARCHAR(MAX) NULL,
    [status] NVARCHAR(20) NOT NULL DEFAULT 'pending',
    [model_name] NVARCHAR(100) NULL,
    [error_message] NVARCHAR(2000) NULL,
    [requested_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [generated_at] DATETIME2 NULL,
    CONSTRAINT [PK_post_summaries] PRIMARY KEY ([post_id]),
    CONSTRAINT [CK_post_summaries_1] CHECK (status IN ('pending','processing','completed','failed','stale')),
    CONSTRAINT [CK_post_summaries_2] CHECK (status <> 'completed' OR (transcript IS NOT NULL AND ai_generated_text IS NOT NULL AND generated_at IS NOT NULL))
);

CREATE TABLE [categories] (
    [id] INT IDENTITY(1,1) NOT NULL,
    [name] NVARCHAR(100) NOT NULL,
    [slug] NVARCHAR(120) NOT NULL,
    [category_type] NVARCHAR(20) NOT NULL,
    [post_category_kind] NVARCHAR(20) NULL,
    [is_active] BIT NOT NULL DEFAULT 1,
    CONSTRAINT [PK_categories] PRIMARY KEY ([id]),
    CONSTRAINT [UQ_categories_slug] UNIQUE ([slug]),
    CONSTRAINT [UQ_categories_1] UNIQUE ([category_type], [name]),
    CONSTRAINT [CK_categories_1] CHECK (category_type IN ('post','shop')),
    CONSTRAINT [CK_categories_2] CHECK ((category_type = 'shop' AND post_category_kind IS NULL) OR (category_type = 'post' AND post_category_kind IS NOT NULL AND post_category_kind IN ('food','recipe','topic')))
);

CREATE TABLE [post_categories] (
    [post_id] BIGINT NOT NULL,
    [category_id] INT NOT NULL,
    CONSTRAINT [PK_post_categories] PRIMARY KEY ([post_id], [category_id])
);

CREATE TABLE [ingredients] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [name] NVARCHAR(100) NOT NULL,
    [default_unit] NVARCHAR(20) NOT NULL,
    [calories_per_100g] DECIMAL(10,2) NULL,
    CONSTRAINT [PK_ingredients] PRIMARY KEY ([id]),
    CONSTRAINT [UQ_ingredients_name] UNIQUE ([name]),
    CONSTRAINT [CK_ingredients_1] CHECK (calories_per_100g >= 0)
);

CREATE TABLE [post_ingredients] (
    [post_id] BIGINT NOT NULL,
    [ingredient_id] BIGINT NOT NULL,
    [quantity] DECIMAL(12,3) NULL,
    [unit] NVARCHAR(20) NOT NULL,
    CONSTRAINT [PK_post_ingredients] PRIMARY KEY ([post_id], [ingredient_id]),
    CONSTRAINT [CK_post_ingredients_1] CHECK (quantity > 0)
);

CREATE TABLE [user_allergies] (
    [user_id] BIGINT NOT NULL,
    [ingredient_id] BIGINT NOT NULL,
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_user_allergies] PRIMARY KEY ([user_id], [ingredient_id])
);

CREATE TABLE [user_available_ingredients] (
    [user_id] BIGINT NOT NULL,
    [ingredient_id] BIGINT NOT NULL,
    [quantity] DECIMAL(12,3) NULL,
    [unit] NVARCHAR(20) NOT NULL,
    [updated_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_user_available_ingredients] PRIMARY KEY ([user_id], [ingredient_id]),
    CONSTRAINT [CK_user_available_ingredients_1] CHECK (quantity >= 0)
);

CREATE TABLE [post_likes] (
    [user_id] BIGINT NOT NULL,
    [post_id] BIGINT NOT NULL,
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_post_likes] PRIMARY KEY ([user_id], [post_id])
);

CREATE TABLE [saved_posts] (
    [user_id] BIGINT NOT NULL,
    [post_id] BIGINT NOT NULL,
    [saved_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_saved_posts] PRIMARY KEY ([user_id], [post_id])
);

CREATE TABLE [comments] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [post_id] BIGINT NOT NULL,
    [user_id] BIGINT NOT NULL,
    [parent_comment_id] BIGINT NULL,
    [content] NVARCHAR(1000) NOT NULL,
    [status] NVARCHAR(20) NOT NULL DEFAULT 'visible',
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [updated_at] DATETIME2 NULL,
    [is_deleted] BIT NOT NULL DEFAULT 0,
    [deleted_at] DATETIME2 NULL,
    CONSTRAINT [PK_comments] PRIMARY KEY ([id]),
    CONSTRAINT [UQ_comments_1] UNIQUE ([post_id], [id]),
    CONSTRAINT [CK_comments_1] CHECK (parent_comment_id IS NULL OR parent_comment_id <> id),
    CONSTRAINT [CK_comments_2] CHECK (status IN ('visible','hidden')),
    CONSTRAINT [CK_comments_3] CHECK ((is_deleted = 0 AND deleted_at IS NULL) OR (is_deleted = 1 AND deleted_at IS NOT NULL))
);

CREATE TABLE [meal_plans] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [user_id] BIGINT NOT NULL,
    [start_date] DATE NOT NULL,
    [end_date] DATE NOT NULL,
    [height_cm] DECIMAL(5,2) NOT NULL,
    [weight_kg] DECIMAL(6,2) NOT NULL,
    [bmi_value] DECIMAL(6,2) NOT NULL,
    [diet_type] NVARCHAR(30) NOT NULL DEFAULT 'vegan',
    [allergies_snapshot] NVARCHAR(MAX) NOT NULL DEFAULT N'[]',
    [available_ingredients_snapshot] NVARCHAR(MAX) NOT NULL DEFAULT N'[]',
    [generation_source] NVARCHAR(20) NOT NULL DEFAULT 'ai',
    [model_name] NVARCHAR(100) NULL,
    [status] NVARCHAR(20) NOT NULL DEFAULT 'pending',
    [error_message] NVARCHAR(2000) NULL,
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [updated_at] DATETIME2 NULL,
    [saved_at] DATETIME2 NULL,
    CONSTRAINT [PK_meal_plans] PRIMARY KEY ([id]),
    CONSTRAINT [CK_meal_plans_1] CHECK (DATEDIFF(DAY, start_date, end_date) = 6),
    CONSTRAINT [CK_meal_plans_2] CHECK (height_cm > 0 AND weight_kg > 0 AND bmi_value > 0),
    CONSTRAINT [CK_meal_plans_3] CHECK (diet_type IN ('vegan','lacto_ovo_vegetarian')),
    CONSTRAINT [CK_meal_plans_4] CHECK (ISJSON(allergies_snapshot) = 1),
    CONSTRAINT [CK_meal_plans_5] CHECK (ISJSON(available_ingredients_snapshot) = 1),
    CONSTRAINT [CK_meal_plans_6] CHECK (generation_source IN ('ai','manual')),
    CONSTRAINT [CK_meal_plans_7] CHECK (status IN ('pending','generating','completed','failed','saved','archived'))
);

CREATE TABLE [meals] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [owner_user_id] BIGINT NULL,
    [combo_name] NVARCHAR(255) NOT NULL,
    [description] NVARCHAR(MAX) NULL,
    [servings] DECIMAL(6,2) NOT NULL DEFAULT 1,
    [total_calories] DECIMAL(10,2) NULL,
    [diet_type] NVARCHAR(30) NOT NULL,
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_meals] PRIMARY KEY ([id]),
    CONSTRAINT [CK_meals_1] CHECK (servings > 0),
    CONSTRAINT [CK_meals_2] CHECK (total_calories >= 0),
    CONSTRAINT [CK_meals_3] CHECK (diet_type IN ('vegan','lacto_ovo_vegetarian'))
);

CREATE TABLE [meal_plan_schedule] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [meal_plan_id] BIGINT NOT NULL,
    [meal_id] BIGINT NOT NULL,
    [day_of_week] TINYINT NOT NULL,
    [meal_time] NVARCHAR(20) NOT NULL,
    [portion_multiplier] DECIMAL(6,2) NOT NULL DEFAULT 1,
    CONSTRAINT [PK_meal_plan_schedule] PRIMARY KEY ([id]),
    CONSTRAINT [UQ_meal_plan_schedule_1] UNIQUE ([meal_plan_id], [day_of_week], [meal_time]),
    CONSTRAINT [CK_meal_plan_schedule_1] CHECK (day_of_week BETWEEN 1 AND 7),
    CONSTRAINT [CK_meal_plan_schedule_2] CHECK (meal_time IN ('breakfast','lunch','dinner')),
    CONSTRAINT [CK_meal_plan_schedule_3] CHECK (portion_multiplier > 0)
);

CREATE TABLE [meal_ingredients] (
    [meal_id] BIGINT NOT NULL,
    [ingredient_id] BIGINT NOT NULL,
    [calculated_quantity] DECIMAL(12,3) NULL,
    [unit] NVARCHAR(20) NOT NULL,
    CONSTRAINT [PK_meal_ingredients] PRIMARY KEY ([meal_id], [ingredient_id]),
    CONSTRAINT [CK_meal_ingredients_1] CHECK (calculated_quantity > 0)
);

CREATE TABLE [chat_sessions] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [user_id] BIGINT NULL,
    [guest_session_id] UNIQUEIDENTIFIER NULL,
    [context_summary] NVARCHAR(MAX) NULL,
    [summary_through_message_id] BIGINT NULL,
    [started_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [last_activity_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_chat_sessions] PRIMARY KEY ([id]),
    CONSTRAINT [CK_chat_sessions_1] CHECK (user_id IS NOT NULL OR guest_session_id IS NOT NULL),
    CONSTRAINT [CK_chat_sessions_2] CHECK ((context_summary IS NULL AND summary_through_message_id IS NULL) OR (context_summary IS NOT NULL AND summary_through_message_id IS NOT NULL AND summary_through_message_id > 0))
);

CREATE TABLE [chat_messages] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [session_id] BIGINT NOT NULL,
    [sender_type] NVARCHAR(20) NOT NULL,
    [message_text] NVARCHAR(MAX) NOT NULL,
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_chat_messages] PRIMARY KEY ([id]),
    CONSTRAINT [CK_chat_messages_1] CHECK (sender_type IN ('user','ai'))
);

CREATE TABLE [ai_usage] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [request_id] UNIQUEIDENTIFIER NOT NULL DEFAULT NEWID(),
    [user_id] BIGINT NULL,
    [guest_session_id] UNIQUEIDENTIFIER NULL,
    [feature_type] NVARCHAR(30) NOT NULL,
    [provider] NVARCHAR(50) NOT NULL,
    [model_name] NVARCHAR(100) NOT NULL,
    [status] NVARCHAR(20) NOT NULL DEFAULT 'pending',
    [input_tokens] INT NULL,
    [output_tokens] INT NULL,
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_ai_usage] PRIMARY KEY ([id]),
    CONSTRAINT [UQ_ai_usage_request_id] UNIQUE ([request_id]),
    CONSTRAINT [CK_ai_usage_1] CHECK (user_id IS NOT NULL OR guest_session_id IS NOT NULL),
    CONSTRAINT [CK_ai_usage_2] CHECK (feature_type IN ('chatbot','meal_planner','video_summary','embedding','chat_summary','content_moderation')),
    CONSTRAINT [CK_ai_usage_3] CHECK (status IN ('pending','completed','failed')),
    CONSTRAINT [CK_ai_usage_4] CHECK (input_tokens >= 0),
    CONSTRAINT [CK_ai_usage_5] CHECK (output_tokens >= 0)
);

CREATE TABLE [post_embeddings] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [post_id] BIGINT NOT NULL,
    [chunk_index] INT NOT NULL,
    [vector_id] NVARCHAR(255) NULL,
    [content_hash] VARCHAR(64) NOT NULL,
    [embedding_model] NVARCHAR(100) NOT NULL,
    [status] NVARCHAR(20) NOT NULL DEFAULT 'pending',
    [embedded_at] DATETIME2 NULL,
    CONSTRAINT [PK_post_embeddings] PRIMARY KEY ([id]),
    CONSTRAINT [UQ_post_embeddings_1] UNIQUE ([post_id], [chunk_index]),
    CONSTRAINT [CK_post_embeddings_1] CHECK (chunk_index >= 0),
    CONSTRAINT [CK_post_embeddings_2] CHECK (status IN ('pending','processing','completed','failed','stale')),
    CONSTRAINT [CK_post_embeddings_3] CHECK (status <> 'completed' OR (vector_id IS NOT NULL AND embedded_at IS NOT NULL))
);

CREATE TABLE [shops] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [suggested_by_user_id] BIGINT NULL,
    [google_place_id] NVARCHAR(255) NULL,
    [name] NVARCHAR(255) NOT NULL,
    [address] NVARCHAR(1000) NULL,
    [latitude] DECIMAL(10,7) NULL,
    [longitude] DECIMAL(10,7) NULL,
    [is_approved] BIT NOT NULL DEFAULT 0,
    [is_deleted] BIT NOT NULL DEFAULT 0,
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    CONSTRAINT [PK_shops] PRIMARY KEY ([id]),
    CONSTRAINT [CK_shops_1] CHECK (latitude BETWEEN -90 AND 90),
    CONSTRAINT [CK_shops_2] CHECK (longitude BETWEEN -180 AND 180),
    CONSTRAINT [CK_shops_3] CHECK ((latitude IS NULL AND longitude IS NULL) OR (latitude IS NOT NULL AND longitude IS NOT NULL))
);

CREATE TABLE [shop_categories] (
    [shop_id] BIGINT NOT NULL,
    [category_id] INT NOT NULL,
    CONSTRAINT [PK_shop_categories] PRIMARY KEY ([shop_id], [category_id])
);

CREATE TABLE [flags] (
    [id] BIGINT IDENTITY(1,1) NOT NULL,
    [reporter_id] BIGINT NULL,
    [post_id] BIGINT NULL,
    [comment_id] BIGINT NULL,
    [shop_id] BIGINT NULL,
    [reason] NVARCHAR(500) NOT NULL,
    [status] NVARCHAR(20) NOT NULL DEFAULT 'pending',
    [resolved_by_admin_id] BIGINT NULL,
    [resolution_note] NVARCHAR(2000) NULL,
    [created_at] DATETIME2 NOT NULL DEFAULT SYSUTCDATETIME(),
    [resolved_at] DATETIME2 NULL,
    [source_type] NVARCHAR(10) NOT NULL DEFAULT 'user',
    [ai_model_name] NVARCHAR(100) NULL,
    CONSTRAINT [PK_flags] PRIMARY KEY ([id]),
    CONSTRAINT [CK_flags_1] CHECK (status IN ('pending','resolved','dismissed')),
    CONSTRAINT [CK_flags_2] CHECK ((CASE WHEN post_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN comment_id IS NOT NULL THEN 1 ELSE 0 END + CASE WHEN shop_id IS NOT NULL THEN 1 ELSE 0 END) = 1),
    CONSTRAINT [CK_flags_3] CHECK ((status = 'pending' AND resolved_by_admin_id IS NULL AND resolved_at IS NULL) OR (status IN ('resolved','dismissed') AND resolved_by_admin_id IS NOT NULL AND resolved_at IS NOT NULL)),
    CONSTRAINT [CK_flags_4] CHECK ((source_type = 'user' AND reporter_id IS NOT NULL AND ai_model_name IS NULL) OR (source_type = 'ai' AND reporter_id IS NULL AND ai_model_name IS NOT NULL AND post_id IS NOT NULL))
);

ALTER TABLE [users] ADD CONSTRAINT [FK_users_1] FOREIGN KEY ([role_id]) REFERENCES [roles] ([id]) ON DELETE NO ACTION;

ALTER TABLE [user_profiles] ADD CONSTRAINT [FK_user_profiles_1] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [user_identities] ADD CONSTRAINT [FK_user_identities_1] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [posts] ADD CONSTRAINT [FK_posts_1] FOREIGN KEY ([author_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [post_media] ADD CONSTRAINT [FK_post_media_1] FOREIGN KEY ([post_id]) REFERENCES [posts] ([id]) ON DELETE NO ACTION;

ALTER TABLE [post_summaries] ADD CONSTRAINT [FK_post_summaries_1] FOREIGN KEY ([post_id]) REFERENCES [posts] ([id]) ON DELETE NO ACTION;

ALTER TABLE [post_summaries] ADD CONSTRAINT [FK_post_summaries_2] FOREIGN KEY ([post_id], [source_media_id]) REFERENCES [post_media] ([post_id], [id]) ON DELETE NO ACTION;

ALTER TABLE [post_categories] ADD CONSTRAINT [FK_post_categories_1] FOREIGN KEY ([post_id]) REFERENCES [posts] ([id]) ON DELETE NO ACTION;

ALTER TABLE [post_categories] ADD CONSTRAINT [FK_post_categories_2] FOREIGN KEY ([category_id]) REFERENCES [categories] ([id]) ON DELETE NO ACTION;

ALTER TABLE [post_ingredients] ADD CONSTRAINT [FK_post_ingredients_1] FOREIGN KEY ([post_id]) REFERENCES [posts] ([id]) ON DELETE NO ACTION;

ALTER TABLE [post_ingredients] ADD CONSTRAINT [FK_post_ingredients_2] FOREIGN KEY ([ingredient_id]) REFERENCES [ingredients] ([id]) ON DELETE NO ACTION;

ALTER TABLE [user_allergies] ADD CONSTRAINT [FK_user_allergies_1] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [user_allergies] ADD CONSTRAINT [FK_user_allergies_2] FOREIGN KEY ([ingredient_id]) REFERENCES [ingredients] ([id]) ON DELETE NO ACTION;

ALTER TABLE [user_available_ingredients] ADD CONSTRAINT [FK_user_available_ingredients_1] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [user_available_ingredients] ADD CONSTRAINT [FK_user_available_ingredients_2] FOREIGN KEY ([ingredient_id]) REFERENCES [ingredients] ([id]) ON DELETE NO ACTION;

ALTER TABLE [post_likes] ADD CONSTRAINT [FK_post_likes_1] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [post_likes] ADD CONSTRAINT [FK_post_likes_2] FOREIGN KEY ([post_id]) REFERENCES [posts] ([id]) ON DELETE NO ACTION;

ALTER TABLE [saved_posts] ADD CONSTRAINT [FK_saved_posts_1] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [saved_posts] ADD CONSTRAINT [FK_saved_posts_2] FOREIGN KEY ([post_id]) REFERENCES [posts] ([id]) ON DELETE NO ACTION;

ALTER TABLE [comments] ADD CONSTRAINT [FK_comments_1] FOREIGN KEY ([post_id]) REFERENCES [posts] ([id]) ON DELETE NO ACTION;

ALTER TABLE [comments] ADD CONSTRAINT [FK_comments_2] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [comments] ADD CONSTRAINT [FK_comments_3] FOREIGN KEY ([post_id], [parent_comment_id]) REFERENCES [comments] ([post_id], [id]) ON DELETE NO ACTION;

ALTER TABLE [meal_plans] ADD CONSTRAINT [FK_meal_plans_1] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [meals] ADD CONSTRAINT [FK_meals_1] FOREIGN KEY ([owner_user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [meal_plan_schedule] ADD CONSTRAINT [FK_meal_plan_schedule_1] FOREIGN KEY ([meal_plan_id]) REFERENCES [meal_plans] ([id]) ON DELETE NO ACTION;

ALTER TABLE [meal_plan_schedule] ADD CONSTRAINT [FK_meal_plan_schedule_2] FOREIGN KEY ([meal_id]) REFERENCES [meals] ([id]) ON DELETE NO ACTION;

ALTER TABLE [meal_ingredients] ADD CONSTRAINT [FK_meal_ingredients_1] FOREIGN KEY ([meal_id]) REFERENCES [meals] ([id]) ON DELETE NO ACTION;

ALTER TABLE [meal_ingredients] ADD CONSTRAINT [FK_meal_ingredients_2] FOREIGN KEY ([ingredient_id]) REFERENCES [ingredients] ([id]) ON DELETE NO ACTION;

ALTER TABLE [chat_sessions] ADD CONSTRAINT [FK_chat_sessions_1] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [chat_messages] ADD CONSTRAINT [FK_chat_messages_1] FOREIGN KEY ([session_id]) REFERENCES [chat_sessions] ([id]) ON DELETE NO ACTION;

ALTER TABLE [ai_usage] ADD CONSTRAINT [FK_ai_usage_1] FOREIGN KEY ([user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [post_embeddings] ADD CONSTRAINT [FK_post_embeddings_1] FOREIGN KEY ([post_id]) REFERENCES [posts] ([id]) ON DELETE NO ACTION;

ALTER TABLE [shops] ADD CONSTRAINT [FK_shops_1] FOREIGN KEY ([suggested_by_user_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [shop_categories] ADD CONSTRAINT [FK_shop_categories_1] FOREIGN KEY ([shop_id]) REFERENCES [shops] ([id]) ON DELETE NO ACTION;

ALTER TABLE [shop_categories] ADD CONSTRAINT [FK_shop_categories_2] FOREIGN KEY ([category_id]) REFERENCES [categories] ([id]) ON DELETE NO ACTION;

ALTER TABLE [flags] ADD CONSTRAINT [FK_flags_1] FOREIGN KEY ([reporter_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

ALTER TABLE [flags] ADD CONSTRAINT [FK_flags_2] FOREIGN KEY ([post_id]) REFERENCES [posts] ([id]) ON DELETE NO ACTION;

ALTER TABLE [flags] ADD CONSTRAINT [FK_flags_3] FOREIGN KEY ([comment_id]) REFERENCES [comments] ([id]) ON DELETE NO ACTION;

ALTER TABLE [flags] ADD CONSTRAINT [FK_flags_4] FOREIGN KEY ([shop_id]) REFERENCES [shops] ([id]) ON DELETE NO ACTION;

ALTER TABLE [flags] ADD CONSTRAINT [FK_flags_5] FOREIGN KEY ([resolved_by_admin_id]) REFERENCES [users] ([id]) ON DELETE NO ACTION;

CREATE INDEX [IX_posts_1] ON [posts] ([status], [is_deleted], [created_at]);

CREATE INDEX [IX_posts_2] ON [posts] ([author_id], [created_at]);

CREATE UNIQUE INDEX [IX_post_media_1] ON [post_media] ([post_id]) WHERE is_primary = 1;

CREATE INDEX [IX_comments_1] ON [comments] ([post_id], [created_at]);

CREATE INDEX [IX_meal_plans_1] ON [meal_plans] ([user_id], [start_date]);

CREATE INDEX [IX_chat_sessions_1] ON [chat_sessions] ([user_id], [last_activity_at]);

CREATE INDEX [IX_chat_sessions_2] ON [chat_sessions] ([guest_session_id], [last_activity_at]);

CREATE INDEX [IX_chat_messages_1] ON [chat_messages] ([session_id], [id]);

CREATE INDEX [IX_ai_usage_1] ON [ai_usage] ([user_id], [feature_type], [created_at]);

CREATE INDEX [IX_ai_usage_2] ON [ai_usage] ([guest_session_id], [feature_type], [created_at]);

CREATE INDEX [IX_post_embeddings_1] ON [post_embeddings] ([status], [post_id]);

CREATE UNIQUE INDEX [IX_shops_1] ON [shops] ([google_place_id]) WHERE google_place_id IS NOT NULL;

CREATE INDEX [IX_flags_1] ON [flags] ([status], [created_at]);

GO
