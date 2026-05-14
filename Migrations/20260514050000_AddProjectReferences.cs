using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace AdalyaSolarAPI.Migrations
{
    public partial class AddProjectReferences : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProjectReferences",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Title = table.Column<string>(type: "text", nullable: false),
                    City = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<string>(type: "text", nullable: false),
                    Capacity = table.Column<string>(type: "text", nullable: false),
                    Panels = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<string>(type: "text", nullable: false),
                    Description = table.Column<string>(type: "text", nullable: false),
                    ImageUrl = table.Column<string>(type: "text", nullable: false),
                    Savings = table.Column<string>(type: "text", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProjectReferences", x => x.Id);
                });

            // Seed the 6 existing static projects
            migrationBuilder.InsertData(
                table: "ProjectReferences",
                columns: new[] { "Title", "City", "Type", "Capacity", "Panels", "Year", "Description", "ImageUrl", "Savings", "IsPublished", "SortOrder", "CreatedAt" },
                values: new object[,]
                {
                    { "Otel Çatı GES Projesi", "Antalya", "Ticari", "120 kWp", 240, "2024", "5 yıldızlı otel kompleksinin tüm çatısına kurulan sistem, aylık elektrik faturasını %70 düşürdü.", "https://images.unsplash.com/photo-1509391366360-2e959784a276?w=600&q=80", "18.000 ₺/ay", true, 1, DateTime.UtcNow },
                    { "Fabrika Çatı Güneş Sistemi", "İstanbul", "Endüstriyel", "250 kWp", 500, "2024", "Tekstil fabrikasının büyük çatı alanında kurulan sistem, yıllık 375.000 kWh üretim sağlıyor.", "https://images.unsplash.com/photo-1497440001374-f26997328c1b?w=600&q=80", "45.000 ₺/ay", true, 2, DateTime.UtcNow },
                    { "Villa Güneş Enerji Sistemi", "İzmir", "Konut", "15 kWp", 30, "2023", "Müstakil konut için tasarlanan hibrit sistem, batarya depolama ile gece de enerji bağımsızlığı sağlıyor.", "https://images.unsplash.com/photo-1508514177221-188b1cf16e9d?w=600&q=80", "4.200 ₺/ay", true, 3, DateTime.UtcNow },
                    { "Tarımsal Sulama GES", "Konya", "Tarımsal", "80 kWp", 160, "2024", "Tarla sulama pompaları için kurulan off-grid sistem, mazot maliyetlerini tamamen ortadan kaldırdı.", "https://images.unsplash.com/photo-1466611653911-95081537e5b7?w=600&q=80", "12.000 ₺/ay", true, 4, DateTime.UtcNow },
                    { "AVM Enerji Projesi", "Bursa", "Ticari", "500 kWp", 1000, "2023", "Alışveriş merkezinin çatısına kurulan dev sistem, binanın ortak alan tüketimini karşılıyor.", "https://images.unsplash.com/photo-1548613053-22087dd8edb8?w=600&q=80", "75.000 ₺/ay", true, 5, DateTime.UtcNow },
                    { "Okul Güneş Paneli Projesi", "Ankara", "Kamu", "30 kWp", 60, "2024", "İlköğretim okulunun elektrik faturasını sıfıra indiren sistem, aynı zamanda öğrencilere eğitim materyali olarak kullanılıyor.", "https://images.unsplash.com/photo-1559302504-64aae6ca6b6d?w=600&q=80", "6.500 ₺/ay", true, 6, DateTime.UtcNow },
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "ProjectReferences");
        }
    }
}
