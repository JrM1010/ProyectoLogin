namespace ProyectoLogin.Models
{
    public class MovimientoInventario
    {
        public int IdMovimiento { get; set; }
        public int IdProducto { get; set; }
        public int IdUsuario { get; set; }
        public string TipoMovimiento { get; set; } // ENTRADA, SALIDA, AJUSTE
        public int Cantidad { get; set; }
        public int StockAnterior { get; set; }
        public int StockNuevo { get; set; }
        public string Motivo { get; set; }
        public DateTime FechaMovimiento { get; set; }

        public virtual Producto Producto { get; set; }
        public virtual Usuario Usuario { get; set; }
    }
}
