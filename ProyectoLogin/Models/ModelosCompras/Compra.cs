using ProyectoLogin.Recursos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.ModelosCompras
{
    public class Compra
    {
        [Key]
        public int IdCompra { get; set; }

        public int IdProveedor { get; set; }
        public DateTime FechaCompra { get; set; } = FechaLocal.Ahora();
        public string? NumeroDocumento { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Subtotal { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal IVA { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal Total { get; set; }
        public string? MetodoPago { get; set; }
        public string? Observaciones { get; set; }
        public string? Estado { get; set; }

        public virtual Proveedor? Proveedor { get; set; }
        public ICollection<DetalleCompra>? Detalles { get; set; }


    }
}
