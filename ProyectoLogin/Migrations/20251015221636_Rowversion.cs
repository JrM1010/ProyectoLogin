using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoLogin.Migrations
{
    /// <inheritdoc />
    public partial class Rowversion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductoPrecio_IdProducto",
                table: "ProductoPrecio");

            migrationBuilder.AddColumn<byte[]>(
                name: "RowVersion",
                table: "ProductoPrecio",
                type: "rowversion",
                rowVersion: true,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProductoPrecio_IdProducto_FechaInicio",
                table: "ProductoPrecio",
                columns: new[] { "IdProducto", "FechaInicio" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ProductoPrecio_IdProducto_FechaInicio",
                table: "ProductoPrecio");

            migrationBuilder.DropColumn(
                name: "RowVersion",
                table: "ProductoPrecio");

            migrationBuilder.CreateIndex(
                name: "IX_ProductoPrecio_IdProducto",
                table: "ProductoPrecio",
                column: "IdProducto");
        }
    }
}
