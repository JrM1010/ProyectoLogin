using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoLogin.Migrations
{
    /// <inheritdoc />
    public partial class addprecioporpresentacion : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "PrecioVentaCaja",
                table: "ProductoPrecio",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioVentaPaquete",
                table: "ProductoPrecio",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioVentaUnidad",
                table: "ProductoPrecio",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "PrecioVentaCaja",
                table: "ProductoPrecio");

            migrationBuilder.DropColumn(
                name: "PrecioVentaPaquete",
                table: "ProductoPrecio");

            migrationBuilder.DropColumn(
                name: "PrecioVentaUnidad",
                table: "ProductoPrecio");
        }
    }
}
