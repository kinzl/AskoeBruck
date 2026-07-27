using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TennisDb.Migrations
{
    /// <inheritdoc />
    public partial class AddPartnerToReservation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PartnerId",
                table: "Reservations",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Reservations_PartnerId",
                table: "Reservations",
                column: "PartnerId");

            migrationBuilder.AddForeignKey(
                name: "FK_Reservations_Players_PartnerId",
                table: "Reservations",
                column: "PartnerId",
                principalTable: "Players",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Reservations_Players_PartnerId",
                table: "Reservations");

            migrationBuilder.DropIndex(
                name: "IX_Reservations_PartnerId",
                table: "Reservations");

            migrationBuilder.DropColumn(
                name: "PartnerId",
                table: "Reservations");
        }
    }
}
