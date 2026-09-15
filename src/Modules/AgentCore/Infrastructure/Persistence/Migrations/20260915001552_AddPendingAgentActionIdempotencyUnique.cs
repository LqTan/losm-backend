using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentCore.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPendingAgentActionIdempotencyUnique : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_PendingAgentActions_IdempotencyKey",
                table: "PendingAgentActions");

            migrationBuilder.CreateIndex(
                name: "UX_PendingAgentActions_UserId_IdempotencyKey",
                table: "PendingAgentActions",
                column: "IdempotencyKey",
                unique: true,
                filter: "[IdempotencyKey] IS NOT NULL");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "UX_PendingAgentActions_UserId_IdempotencyKey",
                table: "PendingAgentActions");

            migrationBuilder.CreateIndex(
                name: "IX_PendingAgentActions_IdempotencyKey",
                table: "PendingAgentActions",
                column: "IdempotencyKey");
        }
    }
}
