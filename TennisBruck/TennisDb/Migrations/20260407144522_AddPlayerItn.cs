using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace TennisDb.Migrations
{
    /// <inheritdoc />
    public partial class AddPlayerItn : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Itn",
                table: "Players",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "LastItnUpdate",
                table: "Players",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "NuLigaPlayerUrl",
                table: "Players",
                type: "text",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "Itn",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "LastItnUpdate",
                table: "Players");

            migrationBuilder.DropColumn(
                name: "NuLigaPlayerUrl",
                table: "Players");
        }
    }
}
