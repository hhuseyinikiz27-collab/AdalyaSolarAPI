using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdalyaSolarAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddFlashSaleNotify : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "FlashSaleNotify",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "FlashSaleNotify",
                table: "Users");
        }
    }
}
