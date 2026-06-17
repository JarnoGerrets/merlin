using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Merlin.Backend.Migrations
{
    /// <inheritdoc />
    public partial class InitialMerlinMemoryPersistence : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "concepts",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 160, nullable: false),
                    ConceptType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    ParentConceptId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concepts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_concepts_concepts_ParentConceptId",
                        column: x => x.ParentConceptId,
                        principalTable: "concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "conversations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 240, nullable: true),
                    ActiveTopic = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversations", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "memories",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    MemoryType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 240, nullable: true),
                    Content = table.Column<string>(type: "TEXT", nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    Project = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    Topic = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    Importance = table.Column<double>(type: "REAL", nullable: false),
                    Confidence = table.Column<double>(type: "REAL", nullable: false),
                    UserConfirmed = table.Column<bool>(type: "INTEGER", nullable: false),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    LastAccessedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    ExpiresAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true),
                    Source = table.Column<string>(type: "TEXT", maxLength: 120, nullable: true),
                    SourceConversationId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    SourceTurnId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memories", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "concept_edges",
                columns: table => new
                {
                    FromConceptId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ToConceptId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    RelationType = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_concept_edges", x => new { x.FromConceptId, x.ToConceptId, x.RelationType });
                    table.ForeignKey(
                        name: "FK_concept_edges_concepts_FromConceptId",
                        column: x => x.FromConceptId,
                        principalTable: "concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_concept_edges_concepts_ToConceptId",
                        column: x => x.ToConceptId,
                        principalTable: "concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "conversation_topics",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 240, nullable: false),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    Status = table.Column<string>(type: "TEXT", maxLength: 40, nullable: false),
                    StartedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    EndedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_conversation_topics", x => x.Id);
                    table.ForeignKey(
                        name: "FK_conversation_topics_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "memory_concepts",
                columns: table => new
                {
                    MemoryId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    ConceptId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    Weight = table.Column<double>(type: "REAL", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_memory_concepts", x => new { x.MemoryId, x.ConceptId });
                    table.ForeignKey(
                        name: "FK_memory_concepts_concepts_ConceptId",
                        column: x => x.ConceptId,
                        principalTable: "concepts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_memory_concepts_memories_MemoryId",
                        column: x => x.MemoryId,
                        principalTable: "memories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "assistant_turns",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    TopicId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    OriginalUserMessage = table.Column<string>(type: "TEXT", nullable: false),
                    GeneratedTextSoFar = table.Column<string>(type: "TEXT", nullable: true),
                    SpokenTextSoFar = table.Column<string>(type: "TEXT", nullable: true),
                    State = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    InterruptionReason = table.Column<string>(type: "TEXT", maxLength: 160, nullable: true),
                    InterruptedByUserMessage = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    UpdatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false),
                    CompletedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_assistant_turns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_assistant_turns_conversation_topics_TopicId",
                        column: x => x.TopicId,
                        principalTable: "conversation_topics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_assistant_turns_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "prompt_compilations",
                columns: table => new
                {
                    Id = table.Column<string>(type: "TEXT", nullable: false),
                    ConversationId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: false),
                    TurnId = table.Column<string>(type: "TEXT", maxLength: 80, nullable: true),
                    PromptType = table.Column<string>(type: "TEXT", maxLength: 60, nullable: false),
                    CompiledPrompt = table.Column<string>(type: "TEXT", nullable: false),
                    EstimatedInputTokens = table.Column<int>(type: "INTEGER", nullable: true),
                    IncludedMemoryIdsJson = table.Column<string>(type: "TEXT", nullable: true),
                    IncludedConceptIdsJson = table.Column<string>(type: "TEXT", nullable: true),
                    CreatedAt = table.Column<DateTimeOffset>(type: "TEXT", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_prompt_compilations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_prompt_compilations_assistant_turns_TurnId",
                        column: x => x.TurnId,
                        principalTable: "assistant_turns",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_prompt_compilations_conversations_ConversationId",
                        column: x => x.ConversationId,
                        principalTable: "conversations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_assistant_turns_ConversationId",
                table: "assistant_turns",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_assistant_turns_CreatedAt",
                table: "assistant_turns",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_assistant_turns_State",
                table: "assistant_turns",
                column: "State");

            migrationBuilder.CreateIndex(
                name: "IX_assistant_turns_TopicId",
                table: "assistant_turns",
                column: "TopicId");

            migrationBuilder.CreateIndex(
                name: "IX_concept_edges_RelationType",
                table: "concept_edges",
                column: "RelationType");

            migrationBuilder.CreateIndex(
                name: "IX_concept_edges_ToConceptId",
                table: "concept_edges",
                column: "ToConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_concepts_ConceptType",
                table: "concepts",
                column: "ConceptType");

            migrationBuilder.CreateIndex(
                name: "IX_concepts_Name",
                table: "concepts",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_concepts_ParentConceptId",
                table: "concepts",
                column: "ParentConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_topics_ConversationId",
                table: "conversation_topics",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_topics_StartedAt",
                table: "conversation_topics",
                column: "StartedAt");

            migrationBuilder.CreateIndex(
                name: "IX_conversation_topics_Status",
                table: "conversation_topics",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_CreatedAt",
                table: "conversations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_Status",
                table: "conversations",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_conversations_UpdatedAt",
                table: "conversations",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_memories_CreatedAt",
                table: "memories",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_memories_ExpiresAt",
                table: "memories",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_memories_Importance",
                table: "memories",
                column: "Importance");

            migrationBuilder.CreateIndex(
                name: "IX_memories_MemoryType",
                table: "memories",
                column: "MemoryType");

            migrationBuilder.CreateIndex(
                name: "IX_memories_Project",
                table: "memories",
                column: "Project");

            migrationBuilder.CreateIndex(
                name: "IX_memories_SourceConversationId",
                table: "memories",
                column: "SourceConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_memories_SourceTurnId",
                table: "memories",
                column: "SourceTurnId");

            migrationBuilder.CreateIndex(
                name: "IX_memories_Topic",
                table: "memories",
                column: "Topic");

            migrationBuilder.CreateIndex(
                name: "IX_memory_concepts_ConceptId",
                table: "memory_concepts",
                column: "ConceptId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_compilations_ConversationId",
                table: "prompt_compilations",
                column: "ConversationId");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_compilations_CreatedAt",
                table: "prompt_compilations",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_compilations_PromptType",
                table: "prompt_compilations",
                column: "PromptType");

            migrationBuilder.CreateIndex(
                name: "IX_prompt_compilations_TurnId",
                table: "prompt_compilations",
                column: "TurnId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "concept_edges");

            migrationBuilder.DropTable(
                name: "memory_concepts");

            migrationBuilder.DropTable(
                name: "prompt_compilations");

            migrationBuilder.DropTable(
                name: "concepts");

            migrationBuilder.DropTable(
                name: "memories");

            migrationBuilder.DropTable(
                name: "assistant_turns");

            migrationBuilder.DropTable(
                name: "conversation_topics");

            migrationBuilder.DropTable(
                name: "conversations");
        }
    }
}
