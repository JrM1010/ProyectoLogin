using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosProducts
{
    public class ProductoProveedor
    {
        [Key]
        public int IdProductoProveedor { get; set; }
        public int IdProducto { get; set; }
        public int IdProveedor { get; set; }
        public decimal? CostoCompra { get; set; }
        public DateTime? FechaUltimaCompra { get; set; }

        public virtual ProductoCore Producto { get; set; }
        public virtual Proveedor Proveedor { get; set; }
    }
}
