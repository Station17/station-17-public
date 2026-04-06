using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    public partial class HL2RPCharacterPersistence : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_locked",
                table: "profile",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "character_inventory_snapshot",
                columns: table => new
                {
                    id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    slot = table.Column<int>(type: "INTEGER", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false),
                    updated_at = table.Column<DateTime>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_inventory_snapshot", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_inventory_snapshot_user_id_slot",
                table: "character_inventory_snapshot",
                columns: new[] { "user_id", "slot" },
                unique: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_inventory_snapshot");

            migrationBuilder.DropColumn(
                name: "is_locked",
                table: "profile");
        }
    }
}
