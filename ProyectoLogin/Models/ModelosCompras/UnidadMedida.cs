using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosCompras
{
    public class UnidadMedida
    {

        [Key]
        public int IdUnidad { get; set; }

        [Required]
        [StringLength(50)]
        public string Nombre { get; set; }

        
        public decimal? FactorConversion { get; set; }

        public virtual ICollection<DetalleCompra>? DetallesCompra { get; set; }
    }
}
