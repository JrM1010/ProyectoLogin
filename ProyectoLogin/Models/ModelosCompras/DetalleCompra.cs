using ProyectoLogin.Models.ModelosProducts;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.ModelosCompras
{
    public class DetalleCompra
    {

        [Key]
        public int IdDetalle { get; set; }

        [ForeignKey("Compra")]
        public int IdCompra { get; set; }

        [ForeignKey("Producto")]
        public int IdProducto { get; set; }

        [ForeignKey("Unidad")]
        public int IdUnidad { get; set; }

        public decimal Cantidad { get; set; }
        public decimal PrecioUnitario { get; set; }
        public decimal Subtotal { get; set; }

        public virtual Compra? Compra { get; set; }
        public virtual ProductoCore? Producto { get; set; }
        public virtual UnidadMedida? Unidad { get; set; }

    }
}
