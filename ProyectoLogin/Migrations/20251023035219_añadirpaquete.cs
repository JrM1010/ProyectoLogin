using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoLogin.Migrations
{
    /// <inheritdoc />
    public partial class añadirpaquete : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "CantidadEquivalente",
                table: "DetalleCompra",
                type: "int",
                nullable: false,
                defaultValue: 0);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "CantidadEquivalente",
                table: "DetalleCompra");
        }
    }
}
