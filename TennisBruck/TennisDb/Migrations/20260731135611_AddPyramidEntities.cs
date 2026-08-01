using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TennisDb.Migrations
{
    /// <inheritdoc />
    public partial class AddPyramidEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPyramid",
                table: "Competitions",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "PyramidChallenges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompetitionId = table.Column<int>(type: "integer", nullable: false),
                    ChallengerTeamId = table.Column<int>(type: "integer", nullable: false),
                    DefenderTeamId = table.Column<int>(type: "integer", nullable: false),
                    ChallengeDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    MatchDate = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    WinnerTeamId = table.Column<int>(type: "integer", nullable: true),
                    Score = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PyramidChallenges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PyramidChallenges_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PyramidChallenges_Teams_ChallengerTeamId",
                        column: x => x.ChallengerTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PyramidChallenges_Teams_DefenderTeamId",
                        column: x => x.DefenderTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PyramidChallenges_Teams_WinnerTeamId",
                        column: x => x.WinnerTeamId,
                        principalTable: "Teams",
                        principalColumn: "Id");
                });

            migrationBuilder.CreateTable(
                name: "PyramidRanks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    CompetitionId = table.Column<int>(type: "integer", nullable: false),
                    Rank = table.Column<int>(type: "integer", nullable: false),
                    TeamId = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PyramidRanks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PyramidRanks_Competitions_CompetitionId",
                        column: x => x.CompetitionId,
                        principalTable: "Competitions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PyramidRanks_Teams_TeamId",
                        column: x => x.TeamId,
                        principalTable: "Teams",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PyramidChallenges_ChallengerTeamId",
                table: "PyramidChallenges",
                column: "ChallengerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PyramidChallenges_CompetitionId",
                table: "PyramidChallenges",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PyramidChallenges_DefenderTeamId",
                table: "PyramidChallenges",
                column: "DefenderTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PyramidChallenges_WinnerTeamId",
                table: "PyramidChallenges",
                column: "WinnerTeamId");

            migrationBuilder.CreateIndex(
                name: "IX_PyramidRanks_CompetitionId",
                table: "PyramidRanks",
                column: "CompetitionId");

            migrationBuilder.CreateIndex(
                name: "IX_PyramidRanks_TeamId",
                table: "PyramidRanks",
                column: "TeamId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PyramidChallenges");

            migrationBuilder.DropTable(
                name: "PyramidRanks");

            migrationBuilder.DropColumn(
                name: "IsPyramid",
                table: "Competitions");
        }
    }
}
