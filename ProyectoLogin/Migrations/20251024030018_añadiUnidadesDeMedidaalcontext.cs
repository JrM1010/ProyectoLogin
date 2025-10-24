using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace ProyectoLogin.Migrations
{
    /// <inheritdoc />
    public partial class añadiUnidadesDeMedidaalcontext : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "UnidadesMedida",
                type: "nvarchar(50)",
                maxLength: 50,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(max)");

            migrationBuilder.InsertData(
                table: "UnidadesMedida",
                columns: new[] { "IdUnidad", "Activo", "EquivalenciaEnUnidades", "Nombre" },
                values: new object[,]
                {
                    { 1, true, 1, "Unidad" },
                    { 2, true, 6, "Paquete" },
                    { 3, true, 12, "Caja" }
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DeleteData(
                table: "UnidadesMedida",
                keyColumn: "IdUnidad",
                keyValue: 1);

            migrationBuilder.DeleteData(
                table: "UnidadesMedida",
                keyColumn: "IdUnidad",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "UnidadesMedida",
                keyColumn: "IdUnidad",
                keyValue: 3);

            migrationBuilder.AlterColumn<string>(
                name: "Nombre",
                table: "UnidadesMedida",
                type: "nvarchar(max)",
                nullable: false,
                oldClrType: typeof(string),
                oldType: "nvarchar(50)",
                oldMaxLength: 50);
        }
    }
}
