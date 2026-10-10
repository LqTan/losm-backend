using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Places.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddPlaceIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Places_ExternalId",
                table: "Places",
                column: "ExternalId");

            migrationBuilder.CreateIndex(
                name: "IX_Places_Latitude_Longitude",
                table: "Places",
                columns: new[] { "Latitude", "Longitude" });

            migrationBuilder.CreateIndex(
                name: "IX_Places_Source",
                table: "Places",
                column: "Source");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Places_ExternalId",
                table: "Places");

            migrationBuilder.DropIndex(
                name: "IX_Places_Latitude_Longitude",
                table: "Places");

            migrationBuilder.DropIndex(
                name: "IX_Places_Source",
                table: "Places");
        }
    }
}
