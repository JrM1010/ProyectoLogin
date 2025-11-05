using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ProyectoLogin.Migrations
{
    /// <inheritdoc />
    public partial class añadirrelacioncomprasusuario : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Compras_Usuario_UsuarioIdUsuario",
                table: "Compras");

            migrationBuilder.DropIndex(
                name: "IX_Compras_UsuarioIdUsuario",
                table: "Compras");

            migrationBuilder.DropColumn(
                name: "UsuarioIdUsuario",
                table: "Compras");

            migrationBuilder.CreateIndex(
                name: "IX_Compras_IdUsuario",
                table: "Compras",
                column: "IdUsuario");

            migrationBuilder.AddForeignKey(
                name: "FK_Compras_Usuario_IdUsuario",
                table: "Compras",
                column: "IdUsuario",
                principalTable: "Usuario",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Compras_Usuario_IdUsuario",
                table: "Compras");

            migrationBuilder.DropIndex(
                name: "IX_Compras_IdUsuario",
                table: "Compras");

            migrationBuilder.AddColumn<int>(
                name: "UsuarioIdUsuario",
                table: "Compras",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateIndex(
                name: "IX_Compras_UsuarioIdUsuario",
                table: "Compras",
                column: "UsuarioIdUsuario");

            migrationBuilder.AddForeignKey(
                name: "FK_Compras_Usuario_UsuarioIdUsuario",
                table: "Compras",
                column: "UsuarioIdUsuario",
                principalTable: "Usuario",
                principalColumn: "IdUsuario",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
