using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.ModelosProducts
{
    public class ProductoProveedor
    {
        [Key]
        public int IdProductoProveedor { get; set; }

        [Required]
        public int IdProducto { get; set; }

        [Required]
        public int IdProveedor { get; set; }

        public decimal CostoCompra { get; set; }

        public DateTime? FechaUltimaCompra { get; set; }

        [ForeignKey("IdProducto")]
        public ProductoCore? Producto { get; set; }

        [ForeignKey("IdProveedor")]
        public Proveedor? Proveedor { get; set; }

    }
}
