using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Content.Server.Database.Migrations.Postgres
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
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.CreateTable(
                name: "character_history_snapshot",
                columns: table => new
                {
                    character_history_snapshot_id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", Npgsql.EntityFrameworkCore.PostgreSQL.Metadata.NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    user_id = table.Column<Guid>(type: "uuid", nullable: false),
                    slot = table.Column<int>(type: "integer", nullable: false),
                    round_id = table.Column<int>(type: "integer", nullable: false),
                    round_ended_at = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    name = table.Column<string>(type: "text", nullable: false),
                    surname = table.Column<string>(type: "text", nullable: false),
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

            migrationBuilder.RenameColumn(
                name: "id",
                table: "character_inventory_snapshot",
                newName: "character_inventory_snapshot_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "character_inventory_snapshot_id",
                table: "character_inventory_snapshot",
                newName: "id");

            migrationBuilder.DropTable(
                name: "character_history_snapshot");

            migrationBuilder.DropColumn(
                name: "is_permanently_dead",
                table: "profile");
        }
    }
}
