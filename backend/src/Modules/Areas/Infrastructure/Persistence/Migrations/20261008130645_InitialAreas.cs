using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Areas.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialAreas : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Areas",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Name = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: false),
                    Level = table.Column<int>(type: "int", nullable: false),
                    ParentId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    ExternalCode = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BboxMinLat = table.Column<double>(type: "float", nullable: false),
                    BboxMinLng = table.Column<double>(type: "float", nullable: false),
                    BboxMaxLat = table.Column<double>(type: "float", nullable: false),
                    BboxMaxLng = table.Column<double>(type: "float", nullable: false),
                    IsActive = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Areas", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Areas_Areas_ParentId",
                        column: x => x.ParentId,
                        principalTable: "Areas",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Areas_ExternalCode",
                table: "Areas",
                column: "ExternalCode");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_Level_ParentId",
                table: "Areas",
                columns: new[] { "Level", "ParentId" });

            migrationBuilder.CreateIndex(
                name: "IX_Areas_Name",
                table: "Areas",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_Areas_ParentId",
                table: "Areas",
                column: "ParentId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Areas");
        }
    }
}
