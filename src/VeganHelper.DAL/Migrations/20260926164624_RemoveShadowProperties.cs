using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace VeganHelper.DAL.Migrations
{
    /// <inheritdoc />
    public partial class RemoveShadowProperties : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
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
                name: "PostId1",
                table: "post_media");

            migrationBuilder.DropColumn(
                name: "PostId1",
                table: "post_ingredients");

            migrationBuilder.DropColumn(
                name: "PostId1",
                table: "post_categories");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
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
    }
}
