using ProyectoLogin.Recursos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.ModelosProducts
{
    [Table("Inventario")]
    public class Inventario
    {

        [Key]
        public int IdInventario { get; set; }

        [ForeignKey("Producto")]
        public int IdProducto { get; set; }

        [Required]
        [Display(Name = "Stock Actual")]
        public int StockActual { get; set; }

        [Display(Name = "Stock Mínimo")]
        public int StockMinimo { get; set; }

        [Display(Name = "Última actualización")]
        public DateTime FechaUltimaActualizacion { get; set; } = FechaLocal.Ahora();

        public virtual ProductoCore? Producto { get; set; }



    }
}
