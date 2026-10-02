using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Places.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class RenameLongtitudeToLongitube : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Longtitude",
                table: "Places",
                newName: "Longitude");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "Longitude",
                table: "Places",
                newName: "Longtitude");
        }
    }
}
