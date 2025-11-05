using ProyectoLogin.Recursos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.Promociones
{
    public class Kit
    {
        [Key]
        public int IdKit { get; set; }

        [Required(ErrorMessage = "El nombre del kit es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres.")]
        public string Nombre { get; set; } = null!;


        public string? Descripcion { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "El subtotal no puede ser negativo.")]
        public decimal Subtotal { get; set; }      // suma sin descuento
        public decimal DescuentoPct { get; set; }  // ej. 0.02m


        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue, ErrorMessage = "El total no puede ser negativo.")]
        public decimal Total { get; set; }         // subtotal * (1 - descuento)
        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = FechaLocal.Ahora();

        public ICollection<KitDetalle>? Detalles { get; set; }



    }
}
