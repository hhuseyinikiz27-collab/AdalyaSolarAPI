using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdalyaSolarAPI.Migrations
{
    public partial class AddAdminNotesAndReviewPhotos : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "Orders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AdminNote",
                table: "Users",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PhotosJson",
                table: "Reviews",
                type: "text",
                nullable: true);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(name: "AdminNote", table: "Orders");
            migrationBuilder.DropColumn(name: "AdminNote", table: "Users");
            migrationBuilder.DropColumn(name: "PhotosJson", table: "Reviews");
        }
    }
}
