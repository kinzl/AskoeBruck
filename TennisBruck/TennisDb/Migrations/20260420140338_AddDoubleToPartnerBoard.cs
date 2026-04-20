using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TennisDb.Migrations
{
    /// <inheritdoc />
    public partial class AddDoubleToPartnerBoard : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsDouble",
                table: "AvailabilitySlots",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "MatchedWithPlayer2Id",
                table: "AvailabilitySlots",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MatchedWithPlayer3Id",
                table: "AvailabilitySlots",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_MatchedWithPlayer2Id",
                table: "AvailabilitySlots",
                column: "MatchedWithPlayer2Id");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_MatchedWithPlayer3Id",
                table: "AvailabilitySlots",
                column: "MatchedWithPlayer3Id");

            migrationBuilder.CreateIndex(
                name: "IX_AvailabilitySlots_MatchedWithPlayerId",
                table: "AvailabilitySlots",
                column: "MatchedWithPlayerId");

            migrationBuilder.AddForeignKey(
                name: "FK_AvailabilitySlots_Players_MatchedWithPlayer2Id",
                table: "AvailabilitySlots",
                column: "MatchedWithPlayer2Id",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AvailabilitySlots_Players_MatchedWithPlayer3Id",
                table: "AvailabilitySlots",
                column: "MatchedWithPlayer3Id",
                principalTable: "Players",
                principalColumn: "Id");

            migrationBuilder.AddForeignKey(
                name: "FK_AvailabilitySlots_Players_MatchedWithPlayerId",
                table: "AvailabilitySlots",
                column: "MatchedWithPlayerId",
                principalTable: "Players",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_AvailabilitySlots_Players_MatchedWithPlayer2Id",
                table: "AvailabilitySlots");

            migrationBuilder.DropForeignKey(
                name: "FK_AvailabilitySlots_Players_MatchedWithPlayer3Id",
                table: "AvailabilitySlots");

            migrationBuilder.DropForeignKey(
                name: "FK_AvailabilitySlots_Players_MatchedWithPlayerId",
                table: "AvailabilitySlots");

            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_MatchedWithPlayer2Id",
                table: "AvailabilitySlots");

            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_MatchedWithPlayer3Id",
                table: "AvailabilitySlots");

            migrationBuilder.DropIndex(
                name: "IX_AvailabilitySlots_MatchedWithPlayerId",
                table: "AvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "IsDouble",
                table: "AvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "MatchedWithPlayer2Id",
                table: "AvailabilitySlots");

            migrationBuilder.DropColumn(
                name: "MatchedWithPlayer3Id",
                table: "AvailabilitySlots");
        }
    }
}
