using System;
using System.ComponentModel.DataAnnotations;
namespace ProyectoLogin.Models
{
    public class Cliente
    {
        [Key]
        public int IdCliente { get; set; }

        [Required]
        [StringLength(12)]
        [Display(Name = "Nit")]
        public string? Nit { get; set; }  // Numero de Identificación Tributaria

        [Required]
        [StringLength(100)]
        public string Nombres { get; set; }

        [Required]
        [StringLength(100)]
        public string Apellidos { get; set; }

        [EmailAddress]
        [StringLength(150)]
        public string Correo { get; set; }

        [Phone]
        [StringLength(25)]
        public string Telefono { get; set; }

        [StringLength(250)]
        public string Direccion { get; set; }

        public bool Activo { get; set; } = true; // soft-delete por defecto true

        public DateTime FechaCreacion { get; set; } = DateTime.UtcNow;
    }
}
