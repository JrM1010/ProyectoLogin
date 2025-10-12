using ProyectoLogin.Models.ModelosProducts;
using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosCompras
{
    public class DetalleCompra
    {

        [Key]
        public int IdDetalle { get; set; }

        [Required]
        public int IdCompra { get; set; }
        public virtual Compra? Compra { get; set; }

        [Required]
        public int IdProducto { get; set; }

        public virtual ProductoCore? Producto { get; set; }

        [Required]
        public decimal Cantidad { get; set; }

        [Required]
        public int IdUnidad { get; set; }
        public virtual UnidadMedida? Unidad { get; set; }

        [Required]
        public decimal PrecioUnitario { get; set; }

        public decimal Subtotal { get; set; }


    }
}
