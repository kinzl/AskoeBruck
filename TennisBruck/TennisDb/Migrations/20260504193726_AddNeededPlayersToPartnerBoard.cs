using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TennisDb.Migrations
{
    /// <inheritdoc />
    public partial class AddNeededPlayersToPartnerBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "NeededPlayers",
                table: "AvailabilitySlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "NeededPlayers",
                table: "AvailabilitySlots");
        }
    }
}
