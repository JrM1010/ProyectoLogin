namespace ProyectoLogin.Models
{
    public class Rol
    {
        public int IdRol { get; set; }
        public string NombreRol { get; set; }

        public virtual ICollection<Usuario> Usuarios { get; set; }



    }
}
