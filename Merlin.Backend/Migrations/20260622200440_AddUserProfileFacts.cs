using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Merlin.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddUserProfileFacts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "user_profile_facts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ProfileId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Key = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    Category = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    DisplayText = table.Column<string>(type: "TEXT", nullable: false),
                    Priority = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<string>(type: "TEXT", nullable: false),
                    LastConfirmedAt = table.Column<string>(type: "TEXT", nullable: true),
                    SourceType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    SourceMemoryId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    SupersedesFactId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    MetadataJson = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_user_profile_facts", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_user_profile_facts_Category",
                table: "user_profile_facts",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_user_profile_facts_Key",
                table: "user_profile_facts",
                column: "Key");

            migrationBuilder.CreateIndex(
                name: "IX_user_profile_facts_ProfileId",
                table: "user_profile_facts",
                column: "ProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_user_profile_facts_ProfileId_Key",
                table: "user_profile_facts",
                columns: new[] { "ProfileId", "Key" },
                unique: true,
                filter: "Status = 'active'");

            migrationBuilder.CreateIndex(
                name: "IX_user_profile_facts_ProfileId_Key_Status",
                table: "user_profile_facts",
                columns: new[] { "ProfileId", "Key", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_user_profile_facts_Status",
                table: "user_profile_facts",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_user_profile_facts_UpdatedAt",
                table: "user_profile_facts",
                column: "UpdatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "user_profile_facts");
        }
    }
}
