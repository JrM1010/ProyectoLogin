using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.ModelosCompras
{
    public class Compra
    {

        [Key]
        public int IdCompra { get; set; }

        [ForeignKey("Proveedor")]
        public int IdProveedor { get; set; }

        public DateTime FechaCompra { get; set; } = DateTime.Now;
        public string? NumeroDocumento { get; set; }
        public decimal Subtotal { get; set; }
        public decimal? IVA { get; set; }
        public decimal Total { get; set; }
        public string MetodoPago { get; set; } = "Efectivo";
        public string? Observaciones { get; set; }
        public string Estado { get; set; } = "Completada";

        public virtual Proveedor? Proveedor { get; set; }
        public virtual ICollection<DetalleCompra>? Detalles { get; set; }

    }
}
