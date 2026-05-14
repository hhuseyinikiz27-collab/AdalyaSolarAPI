using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace AdalyaSolarAPI.Migrations
{
    /// <inheritdoc />
    public partial class AddCouponCampaignId : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CampaignId",
                table: "Coupons",
                type: "integer",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CampaignId",
                table: "Coupons");
        }
    }
}
