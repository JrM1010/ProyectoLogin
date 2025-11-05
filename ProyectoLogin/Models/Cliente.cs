using System;
using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models
{
    public class Cliente
    {
        [Key]
        public int IdCliente { get; set; }

        [Required(ErrorMessage = "El NIT es obligatorio")]
        [StringLength(12, ErrorMessage = "El NIT no puede tener más de 12 caracteres")]
        [RegularExpression(@"^[0-9]+(-[0-9kK])?$", ErrorMessage = "Formato de NIT inválido. Use el formato: 12345678-9")]
        [Display(Name = "NIT")]
        public string? Nit { get; set; }

        [Required(ErrorMessage = "Los nombres son obligatorios")]
        [StringLength(50, ErrorMessage = "Los nombres no pueden tener más de 50 caracteres")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$", ErrorMessage = "Los nombres solo pueden contener letras y espacios")]
        [MinLength(2, ErrorMessage = "Los nombres deben tener al menos 2 caracteres")]
        public string? Nombres { get; set; }

        [Required(ErrorMessage = "Los apellidos son obligatorios")]
        [StringLength(100, ErrorMessage = "Los apellidos no pueden tener más de 100 caracteres")]
        [RegularExpression(@"^[a-zA-ZáéíóúÁÉÍÓÚñÑ\s]+$", ErrorMessage = "Los apellidos solo pueden contener letras y espacios")]
        [MinLength(2, ErrorMessage = "Los apellidos deben tener al menos 2 caracteres")]
        public string? Apellidos { get; set; }

        [EmailAddress(ErrorMessage = "El formato del correo electrónico no es válido")]
        [StringLength(150, ErrorMessage = "El correo no puede tener más de 150 caracteres")]
        [RegularExpression(@"^[^@\s]+@[^@\s]+\.[^@\s]+$", ErrorMessage = "Ingrese un correo electrónico válido")]
        [Display(Name = "Correo Electrónico")]
        public string? Correo { get; set; }

        [StringLength(250, ErrorMessage = "La dirección no puede tener más de 250 caracteres")]
        [Display(Name = "Dirección")]
        public string? Direccion { get; set; }

        public bool Activo { get; set; } = true;

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}