using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Merlin.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddMemoryLifecycleFields : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ArchivedAt",
                table: "memories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CompactContent",
                table: "memories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DeletedAt",
                table: "memories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MemoryAnchorsJson",
                table: "memories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "MergedIntoMemoryId",
                table: "memories",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Status",
                table: "memories",
                type: "TEXT",
                maxLength: 40,
                nullable: false,
                defaultValue: "active");

            migrationBuilder.AddColumn<string>(
                name: "SupersedesMemoryId",
                table: "memories",
                type: "TEXT",
                maxLength: 80,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TagsJson",
                table: "memories",
                type: "TEXT",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_memories_ArchivedAt",
                table: "memories",
                column: "ArchivedAt");

            migrationBuilder.CreateIndex(
                name: "IX_memories_DeletedAt",
                table: "memories",
                column: "DeletedAt");

            migrationBuilder.CreateIndex(
                name: "IX_memories_Status",
                table: "memories",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_memories_Status_MemoryType",
                table: "memories",
                columns: new[] { "Status", "MemoryType" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_memories_ArchivedAt",
                table: "memories");

            migrationBuilder.DropIndex(
                name: "IX_memories_DeletedAt",
                table: "memories");

            migrationBuilder.DropIndex(
                name: "IX_memories_Status",
                table: "memories");

            migrationBuilder.DropIndex(
                name: "IX_memories_Status_MemoryType",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "ArchivedAt",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "CompactContent",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "DeletedAt",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "MemoryAnchorsJson",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "MergedIntoMemoryId",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "Status",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "SupersedesMemoryId",
                table: "memories");

            migrationBuilder.DropColumn(
                name: "TagsJson",
                table: "memories");
        }
    }
}
