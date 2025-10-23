using ProyectoLogin.Models.ModelosProducts;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.Promociones
{
    public class KitDetalle
    {
        [Key]
        public int IdKitDetalle { get; set; }
        public int IdKit { get; set; }
        public Kit? Kit { get; set; }

        public int IdProducto { get; set; }
        public ProductoCore? Producto { get; set; }

        public int Cantidad { get; set; }           // cuántas unidades del producto en el kit
        public decimal PrecioUnitarioSnapshot { get; set; } // precio de venta usado al crear el kit
        
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal => Cantidad * PrecioUnitarioSnapshot;


    }
}
