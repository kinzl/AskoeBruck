using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TennisDb.Migrations
{
    /// <inheritdoc />
    public partial class RemovePlayerUsername : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Username",
                table: "Players");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Username",
                table: "Players",
                type: "text",
                nullable: false,
                defaultValue: "");
        }
    }
}
