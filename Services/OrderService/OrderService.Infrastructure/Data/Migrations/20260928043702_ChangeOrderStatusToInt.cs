using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class ChangeOrderStatusToInt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "StatusTemp",
                table: "Orders",
                type: "int",
                nullable: false,
                defaultValue: 1);

            migrationBuilder.Sql("""
                UPDATE Orders
                SET StatusTemp =
                    CASE Status
                        WHEN 'Pending' THEN 1
                        WHEN 'Confirmed' THEN 2
                        WHEN 'Processing' THEN 3
                        WHEN 'Shipped' THEN 4
                        WHEN 'Delivered' THEN 5
                        WHEN 'Cancelled' THEN 6
                        ELSE 1
                    END
                """);

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "StatusTemp",
                table: "Orders",
                newName: "Status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "StatusTemp",
                table: "Orders",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                defaultValue: "Pending");

            migrationBuilder.Sql("""
                UPDATE Orders
                SET StatusTemp =
                    CASE Status
                        WHEN 1 THEN 'Pending'
                        WHEN 2 THEN 'Confirmed'
                        WHEN 3 THEN 'Processing'
                        WHEN 4 THEN 'Shipped'
                        WHEN 5 THEN 'Delivered'
                        WHEN 6 THEN 'Cancelled'
                        ELSE 'Pending'
                    END
                """);

            migrationBuilder.DropColumn(
                name: "Status",
                table: "Orders");

            migrationBuilder.RenameColumn(
                name: "StatusTemp",
                table: "Orders",
                newName: "Status");
        }
    }
}