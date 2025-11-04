using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models
{
    public class Proveedor
    {
        public int IdProveedor { get; set; }

        [Required(ErrorMessage = "El nombre del proveedor es obligatorio")]
        [StringLength(200, ErrorMessage = "El nombre no puede exceder 200 caracteres")]
        [Display(Name = "Nombre del Proveedor")]
        public string? Nombre { get; set; }

        [StringLength(100, ErrorMessage = "El contacto no puede exceder 100 caracteres")]
        [Display(Name = "Persona de Contacto")]
        public string? Contacto { get; set; }

        [Required(ErrorMessage = "El teléfono es obligatorio")]
        [StringLength(20, ErrorMessage = "El teléfono no puede exceder 20 caracteres")]
        [Phone(ErrorMessage = "Formato de teléfono inválido")]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        [Required(ErrorMessage = "El email es obligatorio")]
        [StringLength(100, ErrorMessage = "El email no puede exceder 100 caracteres")]
        [EmailAddress(ErrorMessage = "Formato de email inválido")]
        [Display(Name = "Correo Electrónico")]
        public string? Email { get; set; }

        [StringLength(500, ErrorMessage = "La dirección no puede exceder 500 caracteres")]
        [Display(Name = "Dirección")]
        public string? Direccion { get; set; }


        public bool Activo { get; set; } = true;

    }
}
