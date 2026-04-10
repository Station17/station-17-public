using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Sqlite
{
    /// <inheritdoc />
    public partial class HL2RPPermadeathCharacterHistory_Auto : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "is_permanently_dead",
                table: "profile",
                type: "INTEGER",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "character_history_snapshot",
                columns: table => new
                {
                    character_history_snapshot_id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    user_id = table.Column<Guid>(type: "TEXT", nullable: false),
                    slot = table.Column<int>(type: "INTEGER", nullable: false),
                    round_id = table.Column<int>(type: "INTEGER", nullable: false),
                    round_ended_at = table.Column<DateTime>(type: "TEXT", nullable: false),
                    name = table.Column<string>(type: "TEXT", nullable: false),
                    surname = table.Column<string>(type: "TEXT", nullable: false),
                    snapshot = table.Column<string>(type: "jsonb", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_character_history_snapshot", x => x.character_history_snapshot_id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_character_history_snapshot_user_id_slot_round_id",
                table: "character_history_snapshot",
                columns: new[] { "user_id", "slot", "round_id" });

        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "character_history_snapshot");

            migrationBuilder.DropColumn(
                name: "is_permanently_dead",
                table: "profile");
        }
    }
}
