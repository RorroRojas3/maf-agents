using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Andes.Agents.Repository.Sql.Migrations
{
    /// <inheritdoc />
    public partial class CreateSessionReportingTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.EnsureSchema(
                name: "Core.Ref");

            migrationBuilder.CreateTable(
                name: "Agent",
                schema: "Core.Ref",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Agent", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Model",
                schema: "Core.Ref",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    DeploymentName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    ProviderName = table.Column<string>(type: "nvarchar(64)", maxLength: 64, nullable: false),
                    InputPricePerMillionTokens = table.Column<decimal>(type: "decimal(19,9)", precision: 19, scale: 9, nullable: false),
                    CachedInputPricePerMillionTokens = table.Column<decimal>(type: "decimal(19,9)", precision: 19, scale: 9, nullable: false),
                    OutputPricePerMillionTokens = table.Column<decimal>(type: "decimal(19,9)", precision: 19, scale: 9, nullable: false),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Model", x => x.Id);
                    table.CheckConstraint("CK_Model_Prices", "[InputPricePerMillionTokens] >= 0 AND [CachedInputPricePerMillionTokens] >= 0 AND [OutputPricePerMillionTokens] >= 0");
                });

            migrationBuilder.CreateTable(
                name: "AgentModelMapping",
                schema: "Core.Ref",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    DateDeactivated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AgentModelMapping", x => x.Id);
                    table.CheckConstraint("CK_AgentModelMapping_ActivationWindow", "[DateDeactivated] IS NULL OR [DateDeactivated] >= [DateCreated]");
                    table.ForeignKey(
                        name: "FK_AgentModelMapping_Agent_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "Core.Ref",
                        principalTable: "Agent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_AgentModelMapping_Model_ModelId",
                        column: x => x.ModelId,
                        principalSchema: "Core.Ref",
                        principalTable: "Model",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Session",
                schema: "Core",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    AgentId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    ModelId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    MessageCount = table.Column<int>(type: "int", nullable: false),
                    InputTokens = table.Column<long>(type: "bigint", nullable: false),
                    CachedInputTokens = table.Column<long>(type: "bigint", nullable: false),
                    OutputTokens = table.Column<long>(type: "bigint", nullable: false),
                    ReasoningTokens = table.Column<long>(type: "bigint", nullable: false),
                    TotalTokens = table.Column<long>(type: "bigint", nullable: false),
                    EstimatedCost = table.Column<decimal>(type: "decimal(19,9)", precision: 19, scale: 9, nullable: false),
                    DateDeleted = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: true),
                    DateCreated = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    DateModified = table.Column<DateTimeOffset>(type: "datetimeoffset", nullable: false),
                    RowVersion = table.Column<byte[]>(type: "rowversion", rowVersion: true, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Session", x => x.Id);
                    table.CheckConstraint("CK_Session_Counts", "[MessageCount] >= 0 AND [InputTokens] >= 0 AND [CachedInputTokens] >= 0 AND [OutputTokens] >= 0 AND [ReasoningTokens] >= 0 AND [TotalTokens] >= 0");
                    table.CheckConstraint("CK_Session_EstimatedCost", "[EstimatedCost] >= 0");
                    table.ForeignKey(
                        name: "FK_Session_Agent_AgentId",
                        column: x => x.AgentId,
                        principalSchema: "Core.Ref",
                        principalTable: "Agent",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Session_Model_ModelId",
                        column: x => x.ModelId,
                        principalSchema: "Core.Ref",
                        principalTable: "Model",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "Agent",
                columns: new[] { "Id", "DateCreated", "DateModified", "Name" },
                values: new object[] { new Guid("34f1c4ff-6232-4001-8c45-3cfa859144e9"), new DateTimeOffset(new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "weather-agent" });

            // Hand-written: the first model and its mapping are operator-owned data, deliberately absent from the EF model.
            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "Model",
                columns: new[] { "Id", "CachedInputPricePerMillionTokens", "DateCreated", "DateModified", "DeploymentName", "InputPricePerMillionTokens", "Name", "OutputPricePerMillionTokens", "ProviderName" },
                values: new object[] { new Guid("31b26e7f-55ea-4e87-b4ab-d85b5ebb66b1"), 0.02m, new DateTimeOffset(new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), "rr-gpt-5.6-luna", 0.20m, "GPT 5.6 Luna", 1.20m, "gpt-5.6-luna" });

            migrationBuilder.InsertData(
                schema: "Core.Ref",
                table: "AgentModelMapping",
                columns: new[] { "Id", "AgentId", "DateCreated", "DateModified", "ModelId" },
                values: new object[] { new Guid("4bf9e1b4-3cbf-4329-bafc-a7b32c8eecc3"), new Guid("34f1c4ff-6232-4001-8c45-3cfa859144e9"), new DateTimeOffset(new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new DateTimeOffset(new DateTime(2026, 9, 11, 0, 0, 0, 0, DateTimeKind.Unspecified), new TimeSpan(0, 0, 0, 0, 0)), new Guid("31b26e7f-55ea-4e87-b4ab-d85b5ebb66b1") });

            migrationBuilder.CreateIndex(
                name: "IX_Agent_Name",
                schema: "Core.Ref",
                table: "Agent",
                column: "Name",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AgentModelMapping_AgentId",
                schema: "Core.Ref",
                table: "AgentModelMapping",
                column: "AgentId",
                unique: true,
                filter: "[DateDeactivated] IS NULL");

            migrationBuilder.CreateIndex(
                name: "IX_AgentModelMapping_AgentId_DateCreated",
                schema: "Core.Ref",
                table: "AgentModelMapping",
                columns: new[] { "AgentId", "DateCreated" });

            migrationBuilder.CreateIndex(
                name: "IX_AgentModelMapping_ModelId",
                schema: "Core.Ref",
                table: "AgentModelMapping",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Model_DeploymentName",
                schema: "Core.Ref",
                table: "Model",
                column: "DeploymentName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Session_AgentId",
                schema: "Core",
                table: "Session",
                column: "AgentId");

            migrationBuilder.CreateIndex(
                name: "IX_Session_DateCreated",
                schema: "Core",
                table: "Session",
                column: "DateCreated");

            migrationBuilder.CreateIndex(
                name: "IX_Session_ModelId",
                schema: "Core",
                table: "Session",
                column: "ModelId");

            migrationBuilder.CreateIndex(
                name: "IX_Session_UserId_SessionId_DateCreated",
                schema: "Core",
                table: "Session",
                columns: new[] { "UserId", "SessionId", "DateCreated" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AgentModelMapping",
                schema: "Core.Ref");

            migrationBuilder.DropTable(
                name: "Session",
                schema: "Core");

            migrationBuilder.DropTable(
                name: "Agent",
                schema: "Core.Ref");

            migrationBuilder.DropTable(
                name: "Model",
                schema: "Core.Ref");
        }
    }
}
