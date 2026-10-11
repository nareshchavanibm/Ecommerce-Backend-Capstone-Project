using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProductService.Infrastructure.Data.Migrations
{
    [DbContext(typeof(AppDbContext))]
    [Migration("20261010000000_BackfillProductImageUrls")]
    public partial class BackfillProductImageUrls : Migration
    {
        private const string ImageHost = "https://loremflickr.com/400/400/";

        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Stock photo per product, keyed on category + first word of the name; lock=Id keeps it stable.
            migrationBuilder.Sql($@"
UPDATE p
SET p.ImageUrl = CONCAT('{ImageHost}', COALESCE(NULLIF(CONCAT_WS(',', NULLIF(b.cw, ''), NULLIF(b.pw, '')), ''), 'product'), '?lock=', p.Id)
FROM Products p
JOIN Categories c ON c.Id = p.CategoryId
CROSS APPLY (SELECT LTRIM(TRANSLATE(LOWER(p.Name), '0123456789&/-_.()+,''""', REPLICATE(' ', 21))) AS pn,
                    LTRIM(TRANSLATE(LOWER(c.Name), '0123456789&/-_.()+,''""', REPLICATE(' ', 21))) AS cn) a
CROSS APPLY (SELECT LEFT(a.pn, CHARINDEX(' ', a.pn + ' ') - 1) AS pw,
                    LEFT(a.cn, CHARINDEX(' ', a.cn + ' ') - 1) AS cw) b
WHERE p.ImageUrl IS NULL OR p.ImageUrl = '';");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql($"UPDATE Products SET ImageUrl = NULL WHERE ImageUrl LIKE '{ImageHost}%';");
        }
    }
}
