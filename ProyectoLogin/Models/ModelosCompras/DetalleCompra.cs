using ProyectoLogin.Models.ModelosProducts;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

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
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }

        public decimal Descuento { get; set; }


        public virtual Compra? Compra { get; set; }
        public virtual ProductoCore? Producto { get; set; }


    }
}
