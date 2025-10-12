using ProyectoLogin.Models.ModelosProducts;
using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosCompras
{
    public class DetalleCompra
    {

        [Key]
        public int IdDetalle { get; set; }

        public int IdCompra { get; set; }
        public int IdProducto { get; set; }
        public decimal Cantidad { get; set; }
        public int IdUnidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }

        public virtual Compra Compra { get; set; }
        public virtual ProductoCore Producto { get; set; }


    }
}
