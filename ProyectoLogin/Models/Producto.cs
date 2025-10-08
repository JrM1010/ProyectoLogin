namespace ProyectoLogin.Models
{
    public class Producto
    {
        public int IdProducto { get; set; }
        public string? Nombre { get; set; }
        public string? Descripcion { get; set; }
        public string? CodigoBarras { get; set; }
        public decimal PrecioCompra { get; set; }
        public decimal PrecioVenta { get; set; }
        public int Stock { get; set; }
        public int StockMinimo { get; set; }
        public bool Activo { get; set; }
        public DateTime FechaCreacion { get; set; }

        // Relaciones
        public int? IdCategoria { get; set; }
        public virtual Categoria? Categoria { get; set; }

        public int? IdMarca { get; set; }
        public virtual Marca? Marca { get; set; }

        public int? IdProveedor { get; set; }
        public virtual Proveedor? Proveedor { get; set; }
    }
}
