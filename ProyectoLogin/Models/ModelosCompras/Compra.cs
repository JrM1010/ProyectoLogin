using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosCompras
{
    public class Compra
    {
        [Key]
        public int IdCompra { get; set; }

        [Required]
        public int IdProveedor { get; set; }
        public virtual ProyectoLogin.Models.Proveedor? Proveedor { get; set; }

        public DateTime FechaCompra { get; set; } = DateTime.Now;

        [StringLength(50)]
        public string? NumeroDocumento { get; set; }

        public decimal Subtotal { get; set; }
        public decimal IVA { get; set; }
        public decimal Total { get; set; }

        [StringLength(50)]
        public string MetodoPago { get; set; } = "Efectivo";

        [StringLength(255)]
        public string? Observaciones { get; set; }

        [StringLength(30)]
        public string Estado { get; set; } = "Completada";

        public virtual ICollection<DetalleCompra>? Detalles { get; set; }


    }
}
