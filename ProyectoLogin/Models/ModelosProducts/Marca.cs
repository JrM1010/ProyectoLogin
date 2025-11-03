using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosProducts
{
    public class Marca
    {
        [Key] 
        public int IdMarca { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        public string Nombre { get; set; }

        public bool Activo { get; set; } = true;


    }
}
