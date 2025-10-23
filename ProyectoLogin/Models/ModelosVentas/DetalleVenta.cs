using ProyectoLogin.Models.ModelosProducts;
using ProyectoLogin.Models.Promociones;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.ModelosVentas
{
    public class DetalleVenta
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)]
        public int IdDetalleVenta { get; set; }

        public int IdVenta { get; set; }
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioUnitario { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Descuento { get; set; } // porcentaje

        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        [ForeignKey("IdVenta")]
        public Venta? Venta { get; set; }

        [ForeignKey("IdProducto")]
        public ProductoCore? Producto { get; set; }


        public int? IdKit { get; set; }
        [ForeignKey("IdKit")]
        public Kit? Kit { get; set; }
    }
}
