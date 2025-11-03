using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models
{
    public class BitacoraMovimiento
    {
        [Key]
        public int IdMovimiento { get; set; }

        [Required]
        public int IdUsuario { get; set; }

        [Required]
        [StringLength(100)]
        public string Accion { get; set; } = null!; // Ej: "Registrar venta"

        [StringLength(255)]
        public string? Descripcion { get; set; }

        [StringLength(50)]
        public string? Modulo { get; set; } // Ej: "Ventas", "Compras"

        [Required]
        public DateTime Fecha { get; set; } = DateTime.UtcNow;

        [ForeignKey("IdUsuario")]
        public Usuario? Usuario { get; set; }
    }
}

