﻿using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace BTCPayServer.Plugins.PodServer.Data.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "BTCPayServer.Plugins.PodServer");

            migrationBuilder.CreateTable(
                name: "Podcasts",
                schema: "BTCPayServer.Plugins.PodServer",
                columns: table => new
                {
                    PodcastId = table.Column<string>(type: "text", nullable: false),
                    UserId = table.Column<string>(type: "text", nullable: true),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    Language = table.Column<string>(type: "text", nullable: false),
                    Category = table.Column<string>(type: "text", nullable: true),
                    MainImage = table.Column<string>(type: "text", nullable: true),
                    Owner = table.Column<string>(type: "text", nullable: true),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Podcasts", x => x.PodcastId);
                });

            migrationBuilder.CreateTable(
                name: "Person",
                schema: "BTCPayServer.Plugins.PodServer",
                columns: table => new
                {
                    PersonId = table.Column<string>(type: "text", nullable: false),
                    PodcastId = table.Column<string>(type: "text", nullable: false),
                    Name = table.Column<string>(type: "text", nullable: false),
                    Email = table.Column<string>(type: "text", nullable: true),
                    Url = table.Column<string>(type: "text", nullable: true),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    ValueRecipient_Type = table.Column<string>(type: "text", nullable: true),
                    ValueRecipient_Address = table.Column<string>(type: "text", nullable: true),
                    ValueRecipient_CustomKey = table.Column<string>(type: "text", nullable: true),
                    ValueRecipient_CustomValue = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Person", x => x.PersonId);
                    table.ForeignKey(
                        name: "FK_Person_Podcasts_PodcastId",
                        column: x => x.PodcastId,
                        principalSchema: "BTCPayServer.Plugins.PodServer",
                        principalTable: "Podcasts",
                        principalColumn: "PodcastId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Season",
                schema: "BTCPayServer.Plugins.PodServer",
                columns: table => new
                {
                    SeasonId = table.Column<string>(type: "text", nullable: false),
                    PodcastId = table.Column<string>(type: "text", nullable: false),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Season", x => x.SeasonId);
                    table.ForeignKey(
                        name: "FK_Season_Podcasts_PodcastId",
                        column: x => x.PodcastId,
                        principalSchema: "BTCPayServer.Plugins.PodServer",
                        principalTable: "Podcasts",
                        principalColumn: "PodcastId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Episodes",
                schema: "BTCPayServer.Plugins.PodServer",
                columns: table => new
                {
                    EpisodeId = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: true),
                    PublishedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    LastUpdatedAt = table.Column<DateTimeOffset>(type: "timestamp with time zone", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: true),
                    Number = table.Column<int>(type: "integer", nullable: false),
                    PodcastId = table.Column<string>(type: "text", nullable: false),
                    SeasonId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Episodes", x => x.EpisodeId);
                    table.ForeignKey(
                        name: "FK_Episodes_Podcasts_PodcastId",
                        column: x => x.PodcastId,
                        principalSchema: "BTCPayServer.Plugins.PodServer",
                        principalTable: "Podcasts",
                        principalColumn: "PodcastId",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Episodes_Season_SeasonId",
                        column: x => x.SeasonId,
                        principalSchema: "BTCPayServer.Plugins.PodServer",
                        principalTable: "Season",
                        principalColumn: "SeasonId");
                });

            migrationBuilder.CreateTable(
                name: "Contribution",
                schema: "BTCPayServer.Plugins.PodServer",
                columns: table => new
                {
                    ContributionId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "text", nullable: true),
                    Split = table.Column<int>(type: "integer", nullable: false),
                    PersonId = table.Column<string>(type: "text", nullable: true),
                    EpisodeId = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Contribution", x => x.ContributionId);
                    table.ForeignKey(
                        name: "FK_Contribution_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalSchema: "BTCPayServer.Plugins.PodServer",
                        principalTable: "Episodes",
                        principalColumn: "EpisodeId");
                    table.ForeignKey(
                        name: "FK_Contribution_Person_PersonId",
                        column: x => x.PersonId,
                        principalSchema: "BTCPayServer.Plugins.PodServer",
                        principalTable: "Person",
                        principalColumn: "PersonId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Enclosure",
                schema: "BTCPayServer.Plugins.PodServer",
                columns: table => new
                {
                    EnclosureId = table.Column<string>(type: "text", nullable: false),
                    EpisodeId = table.Column<string>(type: "text", nullable: false),
                    Url = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Title = table.Column<string>(type: "text", nullable: true),
                    Length = table.Column<int>(type: "integer", nullable: false),
                    IsAlternate = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Enclosure", x => x.EnclosureId);
                    table.ForeignKey(
                        name: "FK_Enclosure_Episodes_EpisodeId",
                        column: x => x.EpisodeId,
                        principalSchema: "BTCPayServer.Plugins.PodServer",
                        principalTable: "Episodes",
                        principalColumn: "EpisodeId",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Contribution_EpisodeId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Contribution",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Contribution_PersonId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Contribution",
                column: "PersonId");

            migrationBuilder.CreateIndex(
                name: "IX_Enclosure_EpisodeId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Enclosure",
                column: "EpisodeId");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_PodcastId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Episodes",
                column: "PodcastId");

            migrationBuilder.CreateIndex(
                name: "IX_Episodes_SeasonId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Episodes",
                column: "SeasonId");

            migrationBuilder.CreateIndex(
                name: "IX_Person_PodcastId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Person",
                column: "PodcastId");

            migrationBuilder.CreateIndex(
                name: "IX_Podcasts_UserId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Podcasts",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Season_PodcastId",
                schema: "BTCPayServer.Plugins.PodServer",
                table: "Season",
                column: "PodcastId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Contribution",
                schema: "BTCPayServer.Plugins.PodServer");

            migrationBuilder.DropTable(
                name: "Enclosure",
                schema: "BTCPayServer.Plugins.PodServer");

            migrationBuilder.DropTable(
                name: "Person",
                schema: "BTCPayServer.Plugins.PodServer");

            migrationBuilder.DropTable(
                name: "Episodes",
                schema: "BTCPayServer.Plugins.PodServer");

            migrationBuilder.DropTable(
                name: "Season",
                schema: "BTCPayServer.Plugins.PodServer");

            migrationBuilder.DropTable(
                name: "Podcasts",
                schema: "BTCPayServer.Plugins.PodServer");
        }
    }
}