using ProyectoLogin.Recursos;
using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.Promociones
{
    public class Kit
    {
        [Key]
        public int IdKit { get; set; }
        public string Nombre { get; set; } = null!;
        public string? Descripcion { get; set; }
        public decimal Subtotal { get; set; }      // suma sin descuento
        public decimal DescuentoPct { get; set; }  // ej. 0.02m
        public decimal Total { get; set; }         // subtotal * (1 - descuento)
        public bool Activo { get; set; } = true;
        public DateTime FechaCreacion { get; set; } = FechaLocal.Ahora();

        public ICollection<KitDetalle>? Detalles { get; set; }



    }
}
