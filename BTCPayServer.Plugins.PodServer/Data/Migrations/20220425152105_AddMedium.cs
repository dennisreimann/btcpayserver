using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.PodServer.Data.Migrations
{
    public partial class AddMedium : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Medium",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Podcasts",
                type: "text",
                nullable: false,
                defaultValue: "");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Medium",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Podcasts");
        }
    }
}
