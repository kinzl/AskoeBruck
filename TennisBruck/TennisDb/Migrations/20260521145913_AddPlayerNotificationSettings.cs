using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace TennisDb.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerNotificationSettings : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PlayerNotificationSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PlayerId = table.Column<int>(type: "integer", nullable: false),
                    EmailOnOpponentAssigned = table.Column<bool>(type: "boolean", nullable: false),
                    EmailOnSlotFull = table.Column<bool>(type: "boolean", nullable: false),
                    EmailOnSlotCancelled = table.Column<bool>(type: "boolean", nullable: false),
                    EmailOnSlotJoined = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PlayerNotificationSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PlayerNotificationSettings_Players_PlayerId",
                        column: x => x.PlayerId,
                        principalTable: "Players",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PlayerNotificationSettings_PlayerId",
                table: "PlayerNotificationSettings",
                column: "PlayerId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PlayerNotificationSettings");
        }
    }
}
