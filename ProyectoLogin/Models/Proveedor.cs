using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models
{
    public class Proveedor
    {
        public int IdProveedor { get; set; }

        [Required(ErrorMessage = "El nombre del proveedor es obligatorio")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres")]
        [Display(Name = "Nombre del Proveedor")]
        public string? Nombre { get; set; }

        [StringLength(50, ErrorMessage = "El nombre del contacto no puede exceder los 50 caracteres")]
        [Display(Name = "Persona de Contacto")]
        public string? Contacto { get; set; }

        [StringLength(20, ErrorMessage = "El teléfono no puede exceder los 20 caracteres")]
        [RegularExpression(@"^[0-9\s\-\+\(\)\.]*$", ErrorMessage = "El formato del teléfono no es válido")]
        [Display(Name = "Teléfono")]
        public string? Telefono { get; set; }

        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
        [StringLength(100, ErrorMessage = "El email no puede exceder los 100 caracteres")]
        [Display(Name = "Correo Electrónico")]
        public string? Email { get; set; }

        [StringLength(200, ErrorMessage = "La dirección no puede exceder los 100 caracteres")]
        [Display(Name = "Dirección")]
        public string? Direccion { get; set; }


        public bool Activo { get; set; } = true;

    }
}
