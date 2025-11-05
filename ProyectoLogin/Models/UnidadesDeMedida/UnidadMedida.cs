using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.UnidadesDeMedida
{
    public class UnidadMedida
    {
        [Key]
        public int IdUnidad { get; set; }
        public string Nombre { get; set; } = string.Empty;

        
        public int EquivalenciaEnUnidades { get; set; } // Ej: 12 si una caja tiene 12 unidades

        [Precision(5, 2)]
        public decimal MargenGanancia { get; set; } = 0.25m; // 25% por defecto


        [Precision(5, 2)]
        public decimal DescuentoAplicable { get; set; } = 0m; // Descuento para el cliente


        public bool Activo { get; set; } = true;


        // 🔁 Relación inversa
        public ICollection<ProductoUnidad>? ProductosUnidades { get; set; }
    }
}
