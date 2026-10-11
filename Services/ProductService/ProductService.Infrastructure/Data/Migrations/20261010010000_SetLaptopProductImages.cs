using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductService.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261010010000_SetLaptopProductImages")]
    public partial class SetLaptopProductImages : Migration
    {
        private const string LaptopImage = "https://images.unsplash.com/photo-1496181133206-80ce9b88a853?auto=format&fit=crop&w=800&q=80";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Replaces only missing or placeholder (loremflickr) images.
            migrationBuilder.Sql($@"
UPDATE p
SET p.ImageUrl = '{LaptopImage}'
FROM Products p
JOIN Categories c ON c.Id = p.CategoryId
WHERE (p.Name LIKE '%laptop%' OR c.Name LIKE '%laptop%')
  AND (p.ImageUrl IS NULL OR p.ImageUrl = '' OR p.ImageUrl LIKE 'https://loremflickr.com/%');");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE Products SET ImageUrl = NULL WHERE ImageUrl = '{LaptopImage}';");
        }
    }
}
