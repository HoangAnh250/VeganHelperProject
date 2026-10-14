using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.Migrations
{
    /// <inheritdoc />
    public partial class AddPostFeedAndFixShadow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DifficultyLevel",
                table: "posts",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PostId1",
                table: "post_media",
                type: "BIGINT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PostId1",
                table: "post_ingredients",
                type: "BIGINT",
                nullable: true);

            migrationBuilder.AddColumn<long>(
                name: "PostId1",
                table: "post_categories",
                type: "BIGINT",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "post_steps",
                columns: table => new
                {
                    id = table.Column<long>(type: "BIGINT", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    post_id = table.Column<long>(type: "BIGINT", nullable: false),
                    step_number = table.Column<int>(type: "INT", nullable: false),
                    description = table.Column<string>(type: "NVARCHAR(MAX)", nullable: false),
                    media_url = table.Column<string>(type: "NVARCHAR(1000)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_post_steps", x => x.id);
                    table.UniqueConstraint("UQ_post_steps_1", x => new { x.post_id, x.step_number });
                    table.CheckConstraint("CK_post_steps_1", "step_number > 0");
                    table.ForeignKey(
                        name: "FK_post_steps_1",
                        column: x => x.post_id,
                        principalTable: "posts",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_post_media_PostId1",
                table: "post_media",
                column: "PostId1");

            migrationBuilder.CreateIndex(
                name: "IX_post_ingredients_PostId1",
                table: "post_ingredients",
                column: "PostId1");

            migrationBuilder.CreateIndex(
                name: "IX_post_categories_PostId1",
                table: "post_categories",
                column: "PostId1");

            migrationBuilder.AddForeignKey(
                name: "FK_post_categories_posts_PostId1",
                table: "post_categories",
                column: "PostId1",
                principalTable: "posts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_post_ingredients_posts_PostId1",
                table: "post_ingredients",
                column: "PostId1",
                principalTable: "posts",
                principalColumn: "id");

            migrationBuilder.AddForeignKey(
                name: "FK_post_media_posts_PostId1",
                table: "post_media",
                column: "PostId1",
                principalTable: "posts",
                principalColumn: "id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_post_categories_posts_PostId1",
                table: "post_categories");

            migrationBuilder.DropForeignKey(
                name: "FK_post_ingredients_posts_PostId1",
                table: "post_ingredients");

            migrationBuilder.DropForeignKey(
                name: "FK_post_media_posts_PostId1",
                table: "post_media");

            migrationBuilder.DropTable(
                name: "post_steps");

            migrationBuilder.DropIndex(
                name: "IX_post_media_PostId1",
                table: "post_media");

            migrationBuilder.DropIndex(
                name: "IX_post_ingredients_PostId1",
                table: "post_ingredients");

            migrationBuilder.DropIndex(
                name: "IX_post_categories_PostId1",
                table: "post_categories");

            migrationBuilder.DropColumn(
                name: "DifficultyLevel",
                table: "posts");

            migrationBuilder.DropColumn(
                name: "PostId1",
                table: "post_media");

            migrationBuilder.DropColumn(
                name: "PostId1",
                table: "post_ingredients");

            migrationBuilder.DropColumn(
                name: "PostId1",
                table: "post_categories");
        }
    }
}
