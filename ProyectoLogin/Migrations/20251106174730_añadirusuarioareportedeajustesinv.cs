using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoLogin.Migrations
{
    /// <inheritdoc />
    public partial class añadirusuarioareportedeajustesinv : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "UsuarioAjuste",
                table: "MovInventario",
                type: "nvarchar(max)",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "UsuarioAjuste",
                table: "MovInventario");
        }
    }
}
