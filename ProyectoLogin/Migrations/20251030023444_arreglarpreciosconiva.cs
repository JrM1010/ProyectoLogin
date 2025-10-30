using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoLogin.Migrations
{
    /// <inheritdoc />
    public partial class arreglarpreciosconiva : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "IVACompra",
                table: "ProductoPrecio",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "MargenGanancia",
                table: "ProductoPrecio",
                type: "decimal(5,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "OrigenCambio",
                table: "ProductoPrecio",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioBase",
                table: "ProductoPrecio",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "PrecioVentaSinIVA",
                table: "ProductoPrecio",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<string>(
                name: "UsuarioRegistro",
                table: "ProductoPrecio",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IVACompra",
                table: "ProductoPrecio");

            migrationBuilder.DropColumn(
                name: "MargenGanancia",
                table: "ProductoPrecio");

            migrationBuilder.DropColumn(
                name: "OrigenCambio",
                table: "ProductoPrecio");

            migrationBuilder.DropColumn(
                name: "PrecioBase",
                table: "ProductoPrecio");

            migrationBuilder.DropColumn(
                name: "PrecioVentaSinIVA",
                table: "ProductoPrecio");

            migrationBuilder.DropColumn(
                name: "UsuarioRegistro",
                table: "ProductoPrecio");
        }
    }
}
