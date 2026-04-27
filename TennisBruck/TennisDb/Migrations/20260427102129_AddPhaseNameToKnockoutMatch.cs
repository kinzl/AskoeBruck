using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TennisDb.Migrations
{
    /// <inheritdoc />
    public partial class AddPhaseNameToKnockoutMatch : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "PhaseName",
                table: "Matches",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PhaseName",
                table: "Matches");
        }
    }
}
