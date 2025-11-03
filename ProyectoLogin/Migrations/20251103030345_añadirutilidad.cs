using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoLogin.Migrations
{
    /// <inheritdoc />
    public partial class añadirutilidad : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "Utilidad",
                table: "DetalleVenta",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.CreateIndex(
                name: "IX_DetalleCompra_IdUnidad",
                table: "DetalleCompra",
                column: "IdUnidad");

            migrationBuilder.AddForeignKey(
                name: "FK_DetalleCompra_UnidadesMedida_IdUnidad",
                table: "DetalleCompra",
                column: "IdUnidad",
                principalTable: "UnidadesMedida",
                principalColumn: "IdUnidad",
                onDelete: ReferentialAction.Cascade);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetalleCompra_UnidadesMedida_IdUnidad",
                table: "DetalleCompra");

            migrationBuilder.DropIndex(
                name: "IX_DetalleCompra_IdUnidad",
                table: "DetalleCompra");

            migrationBuilder.DropColumn(
                name: "Utilidad",
                table: "DetalleVenta");
        }
    }
}
