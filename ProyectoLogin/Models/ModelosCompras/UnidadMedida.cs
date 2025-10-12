using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosCompras
{
    public class UnidadMedida
    {

        [Key]
        public int IdUnidad { get; set; }

        [Required]
        [StringLength(100)]
        public string Nombre { get; set; }

        // Factor de conversión (por ejemplo: 1 para unidad, 12 para caja si una caja = 12 unidades)
        public decimal FactorConversion { get; set; } = 1m;




    }
}
