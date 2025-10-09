using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosProducts
{
    public class Marca
    {
        [Key] // 👈 Esto es lo que soluciona el error
        public int IdMarca { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(100)]
        public string Nombre { get; set; }

        public bool Activo { get; set; } = true;


    }
}
