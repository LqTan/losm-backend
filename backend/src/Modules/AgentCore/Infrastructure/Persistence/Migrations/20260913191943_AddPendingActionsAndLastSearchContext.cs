using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingActionsAndLastSearchContext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "LastSearchContexts",
                columns: table => new
                {
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Query = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CenterLatitude = table.Column<double>(type: "float", nullable: false),
                    CenterLongitude = table.Column<double>(type: "float", nullable: false),
                    RadiusKm = table.Column<double>(type: "float", nullable: false),
                    ResultPlaceIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    FiltersJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LastSearchContexts", x => x.SessionId);
                });

            migrationBuilder.CreateTable(
                name: "PendingAgentActions",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SessionId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    UserId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    PlanId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ActionType = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: false),
                    PayloadJson = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    Description = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    ConfirmationId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    IdempotencyKey = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(30)", maxLength: 30, nullable: false),
                    ResultJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Error = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ConfirmedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PendingAgentActions", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LastSearchContexts_UpdatedAt",
                table: "LastSearchContexts",
                column: "UpdatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_PendingAgentActions_ConfirmationId",
                table: "PendingAgentActions",
                column: "ConfirmationId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingAgentActions_IdempotencyKey",
                table: "PendingAgentActions",
                column: "IdempotencyKey");

            migrationBuilder.CreateIndex(
                name: "IX_PendingAgentActions_SessionId",
                table: "PendingAgentActions",
                column: "SessionId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingAgentActions_UserId",
                table: "PendingAgentActions",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_PendingAgentActions_UserId_Status",
                table: "PendingAgentActions",
                columns: new[] { "UserId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "LastSearchContexts");

            migrationBuilder.DropTable(
                name: "PendingAgentActions");
        }
    }
}
