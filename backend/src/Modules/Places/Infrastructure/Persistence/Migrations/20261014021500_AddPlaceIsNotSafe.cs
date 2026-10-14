using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Places.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceIsNotSafe : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsNotSafe",
                table: "Places",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "NotSafeReason",
                table: "Places",
                type: "nvarchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Places_IsNotSafe",
                table: "Places",
                column: "IsNotSafe");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Places_IsNotSafe",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "IsNotSafe",
                table: "Places");

            migrationBuilder.DropColumn(
                name: "NotSafeReason",
                table: "Places");
        }
    }
}
