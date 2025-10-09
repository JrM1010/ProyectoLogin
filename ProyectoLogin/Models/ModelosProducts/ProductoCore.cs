using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Models.ModelosProducts
{
    public class ProductoCore
    {
        [Key]
        public int IdProducto { get; set; }

        [Required(ErrorMessage = "El nombre es obligatorio")]
        [StringLength(200)]
        public string Nombre { get; set; }

        [StringLength(500)]
        public string? Descripcion { get; set; }

        [Display(Name = "Código de Barras")]
        [StringLength(100)]
        public string? CodigoBarras { get; set; }

        [Required(ErrorMessage = "Debe seleccionar una categoría")]
        [Display(Name = "Categoría")]
        public int IdCategoria { get; set; }

        [Required(ErrorMessage = "Debe seleccionar una marca")]
        [Display(Name = "Marca")]
        public int IdMarca { get; set; }

        public bool Activo { get; set; } = true;

        [Display(Name = "Fecha de Creación")]
        public DateTime FechaCreacion { get; set; } = DateTime.Now;

        // Relaciones
        public virtual Categoria? Categoria { get; set; }
        public virtual Marca? Marca { get; set; }

        public virtual Inventario? Inventario { get; set; }

        // Opcional: colección de precios si las tienes
        //public virtual ICollection<ProductoPrecio>? Precios { get; set; }


    }
}
