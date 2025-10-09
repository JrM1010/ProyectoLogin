using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosProducts
{
    public class Categoria
    {
        [Key] // 👈 asegura a EF que esta es la clave primaria
        public int IdCategoria { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        public string Nombre { get; set; }

        [StringLength(300)]
        public string? Descripcion { get; set; }

        public bool Activo { get; set; } = true;

    }
}
