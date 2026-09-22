using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ECommerce.Infrastructure.Migrations
{
    public partial class SetIsActiveDefaultTrue : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ensure existing products are marked active
            migrationBuilder.Sql("UPDATE Products SET IsActive = 1 WHERE IsActive = 0 OR IsActive IS NULL;");

            // Change default for new rows to true
            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: true,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: false);
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Revert default to false (and optionally set existing values back to false)
            migrationBuilder.AlterColumn<bool>(
                name: "IsActive",
                table: "Products",
                type: "bit",
                nullable: false,
                defaultValue: false,
                oldClrType: typeof(bool),
                oldType: "bit",
                oldDefaultValue: true);

            migrationBuilder.Sql("UPDATE Products SET IsActive = 0 WHERE IsActive = 1;");
        }
    }
}
