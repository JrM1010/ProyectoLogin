using ProyectoLogin.Models.UnidadesDeMedida;
using ProyectoLogin.Recursos;
using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosProducts
{
    public class ProductoCore
    {
        [Key]
        public int IdProducto { get; set; }

        [Required(ErrorMessage = "El nombre del producto es obligatorio.")]
        [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres.")]
        public string? Nombre { get; set; }

        [Required(ErrorMessage = "Las especificaciones son obligatorias.")]
        [StringLength(500, ErrorMessage = "Las especificaciones no puede exceder los 500 caracteres.")]
        public string? Descripcion { get; set; }

        [Display(Name = "Código de Barras")]
        public string? CodigoBarras { get; set; }

        [Required(ErrorMessage = "La categoría es obligatoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una categoría.")]
        [Display(Name = "Categoría")]
        public int IdCategoria { get; set; }

        [Required(ErrorMessage = "La marca es obligatoria.")]
        [Range(1, int.MaxValue, ErrorMessage = "Debe seleccionar una marca.")]
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

        // 🔹 Nueva relación con precios del producto
        public virtual ICollection<ProductoPrecio>? Precios { get; set; }

    }
}
