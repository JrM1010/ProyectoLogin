using ProyectoLogin.Recursos;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace ProyectoLogin.Models.ModelosVentas
{
    public class Venta
    {
        [Key]
        public int IdVenta { get; set; }

        public int? IdCliente { get; set; }
        public int IdUsuario { get; set; }  // Vendedor o Administrador que realiza la venta

        public DateTime FechaVenta { get; set; } = FechaLocal.Ahora();
        public decimal Subtotal { get; set; }
        public decimal IVA { get; set; }
        public decimal Total { get; set; }
        public string? MetodoPago { get; set; }
        public string? Estado { get; set; } = "Completada";

        // Relaciones
        [ForeignKey("IdCliente")]
        public Cliente? Cliente { get; set; }

        [ForeignKey("IdUsuario")]
        public Usuario? Usuario { get; set; }

        public ICollection<DetalleVenta>? Detalles { get; set; }
    }
}
