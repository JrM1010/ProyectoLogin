using ProyectoLogin.Models.UnidadesDeMedida;
using ProyectoLogin.Recursos;
using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosProducts
{
    public class ProductoCore
    {
        [Key]
        public int IdProducto { get; set; }

        public string? Nombre { get; set; }

        public string? Descripcion { get; set; }

        [Display(Name = "Código de Barras")]
        public string? CodigoBarras { get; set; }


        [Display(Name = "Categoría")]
        public int IdCategoria { get; set; }

        [Display(Name = "Marca")]
        public int IdMarca { get; set; }

        public bool Activo { get; set; } = true;

        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; } = FechaLocal.Ahora();   


        // Relaciones
        public virtual Categoria? Categoria { get; set; }
        public virtual Marca? Marca { get; set; }

        public virtual Inventario? Inventario { get; set; }


        //Relación inversa con ProductoUnidad
        public ICollection<ProductoUnidad>? ProductosUnidades { get; set; }


    }
}
