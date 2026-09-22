using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class UpdateAdminSeed : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Email", "PasswordHash" },
                values: new object[] { "admin@e.com", "$2a$11$G5jN7tDC/71CKW3O/uiIEet7s3/z6ANMqNEIH7cdQUDuhvZeen5sm" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.UpdateData(
                table: "Users",
                keyColumn: "Id",
                keyValue: 4,
                columns: new[] { "Email", "PasswordHash" },
                values: new object[] { "admin@ecommerce.com", "$2a$11$H.mfDO5nV2A6rh7GbIQNWOe6GYPHOgj3vuONG6dAK0UxdSAFV18QK" });
        }
    }
}
