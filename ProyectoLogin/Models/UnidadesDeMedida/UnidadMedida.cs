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
        public bool Activo { get; set; } = true;


        // 🔁 Relación inversa
        public ICollection<ProductoUnidad>? ProductosUnidades { get; set; }
    }
}
