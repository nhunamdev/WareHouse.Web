using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace WareHouse.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddProductImageItemAssignment : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ItemId",
                table: "ProductImages",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductImages_ItemId",
                table: "ProductImages",
                column: "ItemId");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductImages_Items_ItemId",
                table: "ProductImages",
                column: "ItemId",
                principalTable: "Items",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductImages_Items_ItemId",
                table: "ProductImages");

            migrationBuilder.DropIndex(
                name: "IX_ProductImages_ItemId",
                table: "ProductImages");

            migrationBuilder.DropColumn(
                name: "ItemId",
                table: "ProductImages");
        }
    }
}
