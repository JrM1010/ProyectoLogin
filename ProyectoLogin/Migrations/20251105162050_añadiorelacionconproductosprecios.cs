using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoLogin.Migrations
{
    /// <inheritdoc />
    public partial class añadiorelacionconproductosprecios : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ProductoCoreIdProducto",
                table: "ProductoPrecio",
                type: "int",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductoPrecio_ProductoCoreIdProducto",
                table: "ProductoPrecio",
                column: "ProductoCoreIdProducto");

            migrationBuilder.AddForeignKey(
                name: "FK_ProductoPrecio_ProductoCore_ProductoCoreIdProducto",
                table: "ProductoPrecio",
                column: "ProductoCoreIdProducto",
                principalTable: "ProductoCore",
                principalColumn: "IdProducto");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_ProductoPrecio_ProductoCore_ProductoCoreIdProducto",
                table: "ProductoPrecio");

            migrationBuilder.DropIndex(
                name: "IX_ProductoPrecio_ProductoCoreIdProducto",
                table: "ProductoPrecio");

            migrationBuilder.DropColumn(
                name: "ProductoCoreIdProducto",
                table: "ProductoPrecio");
        }
    }
}
