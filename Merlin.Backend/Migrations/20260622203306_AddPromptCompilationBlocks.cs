using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Merlin.Backend.Migrations
{
    /// <inheritdoc />
    public partial class AddPromptCompilationBlocks : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CompiledBlocksJson",
                table: "prompt_compilations",
                type: "TEXT",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "IncludedProfileFactIdsJson",
                table: "prompt_compilations",
                type: "TEXT",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CompiledBlocksJson",
                table: "prompt_compilations");

            migrationBuilder.DropColumn(
                name: "IncludedProfileFactIdsJson",
                table: "prompt_compilations");
        }
    }
}
