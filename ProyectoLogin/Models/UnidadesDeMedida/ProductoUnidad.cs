using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models.ModelosProducts;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.UnidadesDeMedida
{
    public class ProductoUnidad
    {
        [Key]
        [DatabaseGenerated(DatabaseGeneratedOption.Identity)] // 🔹 Esto es importante
        public int IdProductoUnidad { get; set; }

        public int IdProducto { get; set; }
        public ProductoCore Producto { get; set; } = null!;

        public int IdUnidad { get; set; }
        public UnidadMedida UnidadMedida { get; set; } = null!;

        
        public int FactorConversion { get; set; } // Ej: 12 si Caja equivale a 12 Unidades
        [Precision(18, 2)]
        public decimal PrecioCompra { get; set; } // (opcional) Si cambia según presentación


    }
}
