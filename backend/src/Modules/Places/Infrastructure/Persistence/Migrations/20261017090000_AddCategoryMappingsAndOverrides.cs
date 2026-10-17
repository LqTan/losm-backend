using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Places.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddCategoryMappingsAndOverrides : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CategoryMappings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    GroupName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: false),
                    Category = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryMappings", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "CategoryOverrides",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Keyword = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    TargetCategory = table.Column<string>(type: "nvarchar(150)", maxLength: 150, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "datetime2", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CategoryOverrides", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_CategoryMappings_Category",
                table: "CategoryMappings",
                column: "Category");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryMappings_GroupName",
                table: "CategoryMappings",
                column: "GroupName");

            migrationBuilder.CreateIndex(
                name: "IX_CategoryMappings_GroupName_Category",
                table: "CategoryMappings",
                columns: new[] { "GroupName", "Category" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_CategoryOverrides_Keyword",
                table: "CategoryOverrides",
                column: "Keyword",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CategoryMappings");

            migrationBuilder.DropTable(
                name: "CategoryOverrides");
        }
    }
}
