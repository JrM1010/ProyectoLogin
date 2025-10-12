using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosCompras
{
    public class Compra
    {
        [Key]
        public int IdCompra { get; set; }

        public int IdProveedor { get; set; }
        public DateTime FechaCompra { get; set; } = DateTime.Now;
        public string? NumeroDocumento { get; set; }
        public decimal Subtotal { get; set; }
        public decimal IVA { get; set; }
        public decimal Total { get; set; }
        public string MetodoPago { get; set; }
        public string? Observaciones { get; set; }
        public string? Estado { get; set; }

        public virtual Proveedor Proveedor { get; set; }
        public virtual ICollection<DetalleCompra>? Detalles { get; set; }


    }
}
