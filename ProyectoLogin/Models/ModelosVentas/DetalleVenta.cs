using ProyectoLogin.Models.ModelosProducts;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.ModelosVentas
{
    public class DetalleVenta
    {
        [Key]
        public int IdDetalleVenta { get; set; }

        public int IdVenta { get; set; }
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Descuento { get; set; } // porcentaje o monto
        public decimal Subtotal { get; set; }

        [ForeignKey("IdVenta")]
        public Venta Venta { get; set; }

        [ForeignKey("IdProducto")]
        public ProductoCore Producto { get; set; }
    }
}
