using System.Text.Json;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
{
    /// <inheritdoc />
    public partial class CharacterPersistenceColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "character_role",
                table: "profile",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "character_locked",
                table: "profile",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<JsonDocument>(
                name: "persisted_inventory",
                table: "profile",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "character_role",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "character_locked",
                table: "profile");

            migrationBuilder.DropColumn(
                name: "persisted_inventory",
                table: "profile");
        }
    }
}
