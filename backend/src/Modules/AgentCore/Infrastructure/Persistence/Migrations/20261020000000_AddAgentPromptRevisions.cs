using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AgentCore.Infrastructure.Persistence.Migrations;

public partial class AddAgentPromptRevisions : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.CreateTable(
            name: "AgentPromptRevisions",
            columns: table => new
            {
                Key = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                Version = table.Column<long>(type: "bigint", nullable: false),
                Content = table.Column<string>(type: "nvarchar(max)", nullable: false),
                ChangedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                ChangedBy = table.Column<Guid>(type: "uniqueidentifier", nullable: true)
            },
            constraints: table => table.PrimaryKey("PK_AgentPromptRevisions", x => new { x.Key, x.Version }));
    }

    protected override void Down(MigrationBuilder migrationBuilder) =>
        migrationBuilder.DropTable(name: "AgentPromptRevisions");
}
