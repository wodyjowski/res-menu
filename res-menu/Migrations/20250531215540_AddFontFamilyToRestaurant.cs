using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace res_menu.Migrations
{
    /// <inheritdoc />
    public partial class AddFontFamilyToRestaurant : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "FontFamily",
                table: "Restaurants",
                type: "character varying(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FontFamily",
                table: "Restaurants");
        }
    }
}
