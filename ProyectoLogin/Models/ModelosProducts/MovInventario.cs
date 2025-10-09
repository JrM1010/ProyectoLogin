using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.ModelosProducts
{
    public class MovInventario
    {

        [Key]
        public int IdMovimiento { get; set; }

        [ForeignKey("Producto")]
        public int IdProducto { get; set; }

        [Required]
        [Display(Name = "Fecha del movimiento")]
        public DateTime Fecha { get; set; } = DateTime.Now;

        [Required]
        [Display(Name = "Cantidad")]
        public decimal Cantidad { get; set; }

        [Required]
        [Display(Name = "Tipo de movimiento")]
        public string TipoMovimiento { get; set; } = ""; // "Entrada" o "Salida"

        [Display(Name = "Referencia")]
        public string? Referencia { get; set; }

        [Display(Name = "Observación")]
        public string? Observacion { get; set; }

        public virtual ProductoCore? Producto { get; set; }


    }
}
