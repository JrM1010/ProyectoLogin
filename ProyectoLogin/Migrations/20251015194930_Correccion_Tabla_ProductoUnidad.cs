using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoLogin.Migrations
{
    /// <inheritdoc />
    public partial class Correccion_Tabla_ProductoUnidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductosUnidades",
                table: "ProductosUnidades");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductosUnidades",
                table: "ProductosUnidades",
                column: "IdProductoUnidad");

            migrationBuilder.CreateIndex(
                name: "IX_ProductosUnidades_IdProducto_IdUnidad",
                table: "ProductosUnidades",
                columns: new[] { "IdProducto", "IdUnidad" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropPrimaryKey(
                name: "PK_ProductosUnidades",
                table: "ProductosUnidades");

            migrationBuilder.DropIndex(
                name: "IX_ProductosUnidades_IdProducto_IdUnidad",
                table: "ProductosUnidades");

            migrationBuilder.AddPrimaryKey(
                name: "PK_ProductosUnidades",
                table: "ProductosUnidades",
                columns: new[] { "IdProducto", "IdUnidad" });
        }
    }
}
