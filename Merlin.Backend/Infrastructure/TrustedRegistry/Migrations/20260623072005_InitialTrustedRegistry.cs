using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Merlin.Backend.Infrastructure.TrustedRegistry.Migrations
{
    /// <inheritdoc />
    public partial class InitialTrustedRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "trusted_app_mappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Alias = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedAlias = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    ExecutablePath = table.Column<string>(type: "TEXT", nullable: false),
                    Source = table.Column<string>(type: "TEXT", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    CreatedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trusted_app_mappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trusted_command_mappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    OriginalCommand = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedOriginalCommand = table.Column<string>(type: "TEXT", nullable: false),
                    Intent = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedCommand = table.Column<string>(type: "TEXT", nullable: false),
                    ToolName = table.Column<string>(type: "TEXT", nullable: false),
                    Target = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trusted_command_mappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trusted_registry_events",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    EventType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityType = table.Column<string>(type: "TEXT", nullable: false),
                    EntityId = table.Column<long>(type: "INTEGER", nullable: true),
                    Alias = table.Column<string>(type: "TEXT", nullable: true),
                    Command = table.Column<string>(type: "TEXT", nullable: true),
                    Target = table.Column<string>(type: "TEXT", nullable: true),
                    ToolName = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trusted_registry_events", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "trusted_url_mappings",
                columns: table => new
                {
                    Id = table.Column<long>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Alias = table.Column<string>(type: "TEXT", nullable: false),
                    NormalizedAlias = table.Column<string>(type: "TEXT", nullable: false),
                    Url = table.Column<string>(type: "TEXT", nullable: false),
                    DisplayName = table.Column<string>(type: "TEXT", nullable: false),
                    CreatedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    LastUsedAtUtc = table.Column<string>(type: "TEXT", nullable: false),
                    UseCount = table.Column<int>(type: "INTEGER", nullable: false),
                    Status = table.Column<string>(type: "TEXT", nullable: false),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_trusted_url_mappings", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_trusted_app_mappings_LastUsedAtUtc",
                table: "trusted_app_mappings",
                column: "LastUsedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_app_mappings_NormalizedAlias",
                table: "trusted_app_mappings",
                column: "NormalizedAlias",
                unique: true,
                filter: "Status = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_app_mappings_Status",
                table: "trusted_app_mappings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_app_mappings_Status_NormalizedAlias",
                table: "trusted_app_mappings",
                columns: new[] { "Status", "NormalizedAlias" });

            migrationBuilder.CreateIndex(
                name: "IX_trusted_app_mappings_UseCount",
                table: "trusted_app_mappings",
                column: "UseCount");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_command_mappings_Intent",
                table: "trusted_command_mappings",
                column: "Intent");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_command_mappings_LastUsedAtUtc",
                table: "trusted_command_mappings",
                column: "LastUsedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_command_mappings_NormalizedOriginalCommand",
                table: "trusted_command_mappings",
                column: "NormalizedOriginalCommand",
                unique: true,
                filter: "Status = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_command_mappings_Status",
                table: "trusted_command_mappings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_command_mappings_Status_NormalizedOriginalCommand",
                table: "trusted_command_mappings",
                columns: new[] { "Status", "NormalizedOriginalCommand" });

            migrationBuilder.CreateIndex(
                name: "IX_trusted_command_mappings_ToolName",
                table: "trusted_command_mappings",
                column: "ToolName");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_command_mappings_UseCount",
                table: "trusted_command_mappings",
                column: "UseCount");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_registry_events_CreatedAtUtc",
                table: "trusted_registry_events",
                column: "CreatedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_registry_events_EntityId",
                table: "trusted_registry_events",
                column: "EntityId");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_registry_events_EntityType",
                table: "trusted_registry_events",
                column: "EntityType");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_registry_events_EventType",
                table: "trusted_registry_events",
                column: "EventType");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_url_mappings_LastUsedAtUtc",
                table: "trusted_url_mappings",
                column: "LastUsedAtUtc");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_url_mappings_NormalizedAlias",
                table: "trusted_url_mappings",
                column: "NormalizedAlias",
                unique: true,
                filter: "Status = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_url_mappings_Status",
                table: "trusted_url_mappings",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_url_mappings_Status_NormalizedAlias",
                table: "trusted_url_mappings",
                columns: new[] { "Status", "NormalizedAlias" });

            migrationBuilder.CreateIndex(
                name: "IX_trusted_url_mappings_Url",
                table: "trusted_url_mappings",
                column: "Url");

            migrationBuilder.CreateIndex(
                name: "IX_trusted_url_mappings_UseCount",
                table: "trusted_url_mappings",
                column: "UseCount");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "trusted_app_mappings");

            migrationBuilder.DropTable(
                name: "trusted_command_mappings");

            migrationBuilder.DropTable(
                name: "trusted_registry_events");

            migrationBuilder.DropTable(
                name: "trusted_url_mappings");
        }
    }
}
