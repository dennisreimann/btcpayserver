using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.PodServer.Data.Migrations
{
    public partial class AddSlugs : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_Episodes_EpisodeId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_Episodes_PodcastId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Episodes");

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Podcasts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Slug",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Episodes",
                type: "text",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Podcasts_Slug",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Podcasts",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_PodcastId_Slug",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Episodes",
                columns: new[] { "PodcastId", "Slug" },
                unique: true);

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_Episodes_EpisodeId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Contributions",
                column: "EpisodeId",
                principalSchema: "BTCPayServer.Plugins.PodServer",
                principalTable: "Episodes",
                principalColumn: "EpisodeId",
                onDelete: ReferentialAction.Cascade);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Contributions_Episodes_EpisodeId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Contributions");

            migrationBuilder.DropIndex(
                name: "IX_Podcasts_Slug",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Podcasts");

            migrationBuilder.DropIndex(
                name: "IX_Episodes_PodcastId_Slug",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Episodes");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Podcasts");

            migrationBuilder.DropColumn(
                name: "Slug",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Episodes");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_PodcastId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Episodes",
                column: "PodcastId");

            migrationBuilder.AddForeignKey(
                name: "FK_Contributions_Episodes_EpisodeId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Contributions",
                column: "EpisodeId",
                principalSchema: "BTCPayServer.Plugins.PodServer",
                principalTable: "Episodes",
                principalColumn: "EpisodeId");
        }
    }
}
