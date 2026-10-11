using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace OrderService.Infrastructure.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddOrderNumber : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "OrderNumber",
                table: "Orders",
                type: "nvarchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "");

            // Existing orders get a random number before the unique index is built.
            // EXEC defers compiling these until the new column exists.
            migrationBuilder.Sql(
                @"EXEC(N'UPDATE Orders
                  SET OrderNumber = ''ORD-'' + UPPER(LEFT(REPLACE(CONVERT(varchar(36), NEWID()), ''-'', ''''), 8))
                  WHERE OrderNumber = ''''')");

            // Stored notifications pointed at the integer id; repoint them at the number.
            migrationBuilder.Sql(
                @"EXEC(N'UPDATE n
                  SET Link = CASE WHEN n.Link LIKE ''/admin/%'' THEN ''/admin/orders/details/'' ELSE ''/orders/'' END + o.OrderNumber,
                      Message = REPLACE(n.Message, ''#'' + CAST(o.Id AS nvarchar(12)), o.OrderNumber)
                  FROM Notifications n
                  JOIN Orders o ON o.Id = n.OrderId')");

            migrationBuilder.CreateIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders",
                column: "OrderNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Orders_OrderNumber",
                table: "Orders");

            migrationBuilder.DropColumn(
                name: "OrderNumber",
                table: "Orders");
        }
    }
}
