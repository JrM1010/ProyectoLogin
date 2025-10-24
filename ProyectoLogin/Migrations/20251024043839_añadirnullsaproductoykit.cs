using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoLogin.Migrations
{
    /// <inheritdoc />
    public partial class añadirnullsaproductoykit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetalleVenta_Kit_IdKit",
                table: "DetalleVenta");

            migrationBuilder.AlterColumn<int>(
                name: "IdProducto",
                table: "DetalleVenta",
                type: "int",
                nullable: true,
                oldClrType: typeof(int),
                oldType: "int");

            migrationBuilder.AddForeignKey(
                name: "FK_DetalleVenta_Kit_IdKit",
                table: "DetalleVenta",
                column: "IdKit",
                principalTable: "Kit",
                principalColumn: "IdKit",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_DetalleVenta_Kit_IdKit",
                table: "DetalleVenta");

            migrationBuilder.AlterColumn<int>(
                name: "IdProducto",
                table: "DetalleVenta",
                type: "int",
                nullable: false,
                defaultValue: 0,
                oldClrType: typeof(int),
                oldType: "int",
                oldNullable: true);

            migrationBuilder.AddForeignKey(
                name: "FK_DetalleVenta_Kit_IdKit",
                table: "DetalleVenta",
                column: "IdKit",
                principalTable: "Kit",
                principalColumn: "IdKit");
        }
    }
}
