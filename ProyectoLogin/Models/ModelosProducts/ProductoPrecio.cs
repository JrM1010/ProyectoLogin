using ProyectoLogin.Recursos;
using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosProducts
{
    public class ProductoPrecio
    {

        [Key]
        public int IdPrecio { get; set; }

        public int IdProducto { get; set; }

        [Required]
        public decimal PrecioCompra { get; set; }

        [Required]
        public decimal PrecioVenta { get; set; }

        public DateTime FechaInicio { get; set; } = FechaLocal.Ahora();
        public DateTime? FechaFin { get; set; } = FechaLocal.Ahora();
        public bool Activo { get; set; } = true;

        public virtual ProductoCore? Producto { get; set; }


    }
}
