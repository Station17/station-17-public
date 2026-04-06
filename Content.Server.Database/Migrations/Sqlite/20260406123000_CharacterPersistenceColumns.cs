using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
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
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "character_locked",
                table: "profile",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "persisted_inventory",
                table: "profile",
                type: "TEXT",
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
