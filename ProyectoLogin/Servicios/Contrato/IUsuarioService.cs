using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;

namespace ProyectoLogin.Servicios.Contrato
{
    // Esta interfaz define el "contrato" que cualquier clase que implemente IUsuarioService debe cumplir.
    // Aquí solo se declara que métodos existen.
    public interface IUsuarioService
    {
        // Método asíncrono que debe devolver un usuario según correo y clave.
        // Se usará principalmente en el LOGIN para validar credenciales.
        Task<Usuario> GetUsuario(string correo, string clave);

        // Método asíncrono que debe guardar un nuevo usuario en la base de datos.
        // Se usará en el REGISTRO para crear cuentas.
        Task<Usuario> SaveUsuario(Usuario modelo);
    }
}
