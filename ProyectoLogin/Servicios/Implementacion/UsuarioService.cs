using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using ProyectoLogin.Models;
using ProyectoLogin.Servicios.Contrato;

namespace ProyectoLogin.Servicios.Implementacion
{
    // Esta clase implementa la interfaz IUsuarioService.
    // en este metodo se buscan o guardan los usuarios en la BD.
    public class UsuarioService : IUsuarioService
    {
        // Campo privado para acceder al contexto de base de datos (Entity Framework).
        private readonly DbpruebaContext _dbContext;

        // Constructor: recibe el DbContext por inyección de dependencias.
        // Esto permite usar la conexión a la base de datos sin crearla manualmente aquí.
        public UsuarioService(DbpruebaContext dbContext)
        {
            _dbContext = dbContext;
        }
        // Método asíncrono que busca un usuario en la BD por correo y clave.
        public async Task<Usuario> GetUsuario(string correo, string clave)
        {
            return await _dbContext.Usuarios
                .Include(u => u.Rol) //Esto carga la relación con la tabla Rol
                .FirstOrDefaultAsync(u => u.Correo == correo && u.Clave == clave);
        }


        // Método asíncrono que guarda un nuevo usuario en la base de datos.
        public async Task<Usuario> SaveUsuario(Usuario modelo) //hola
        {
            // Marca el nuevo usuario para ser agregado a la tabla "Usuarios".
            _dbContext.Usuarios.Add(modelo);

            // Guarda los cambios en la base de datos.
            await _dbContext.SaveChangesAsync();

            return modelo;
        }
    }
}