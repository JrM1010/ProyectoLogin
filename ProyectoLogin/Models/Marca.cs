namespace ProyectoLogin.Models
{
    public class Marca
    {
        public int IdMarca { get; set; }
        public string? Nombre { get; set; }
        public bool Activo { get; set; }

        public virtual ICollection<Producto>? Productos { get; set; }
    }
}
