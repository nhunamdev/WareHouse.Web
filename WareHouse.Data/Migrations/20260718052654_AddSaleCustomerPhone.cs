using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WareHouse.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddSaleCustomerPhone : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CustomerPhone",
                table: "StockDocuments",
                type: "nvarchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.Sql("""
                UPDATE document
                SET document.CustomerPhone = customer.Phone
                FROM StockDocuments AS document
                INNER JOIN Customers AS customer ON customer.Id = document.CustomerId
                WHERE document.DocumentType = 4
                  AND document.CustomerPhone IS NULL
                  AND customer.Phone IS NOT NULL;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CustomerPhone",
                table: "StockDocuments");
        }
    }
}
