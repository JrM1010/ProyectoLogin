using ProyectoLogin.Recursos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.ModelosProducts
{
    public class ProductoPrecio
    {
        [Key]
        public int IdPrecio { get; set; }

        public int IdProducto { get; set; }

        // 🔹 Precio de compra del proveedor (IVA incluido)
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioCompra { get; set; }

        // 🔹 Precio base sin IVA (derivado de la compra)
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioBase { get; set; }

        // 🔹 IVA correspondiente a la compra
        [Column(TypeName = "decimal(18,2)")]
        public decimal IVACompra { get; set; }

        // 🔹 Margen de ganancia aplicado sobre el precio base
        [Column(TypeName = "decimal(5,2)")]
        public decimal MargenGanancia { get; set; }

        // 🔹 Precio de venta sin IVA
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioVentaSinIVA { get; set; }

        // 🔹 Precio final con IVA incluido (este se usa en el POS)
        [Required]
        [Column(TypeName = "decimal(18,2)")]
        public decimal PrecioVenta { get; set; }

        public DateTime FechaInicio { get; set; } = FechaLocal.Ahora();
        public DateTime? FechaFin { get; set; }
        public bool Activo { get; set; } = true;

        // 🔹 Auditoría opcional
        [MaxLength(50)]
        public string? UsuarioRegistro { get; set; }

        [MaxLength(50)]
        public string? OrigenCambio { get; set; } // Ejemplo: "Compra", "Ajuste manual"

        public virtual ProductoCore? Producto { get; set; }
    }
}
