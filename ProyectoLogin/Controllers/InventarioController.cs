using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosProducts;

namespace ProyectoLogin.Controllers
{
    public class InventarioController : Controller
    {
        private readonly DbPruebaContext _context;

        public InventarioController(DbPruebaContext context)
        {
            _context = context;
        }

        // 🔹 LISTA DE INVENTARIO
        public async Task<IActionResult> Index(string? q)
        {
            var inventario = _context.Inventarios
            .Include(i => i.Producto)
            .AsQueryable();

            if (!string.IsNullOrEmpty(q))
                inventario = inventario.Where(i => i.Producto.Nombre.Contains(q));

            ViewBag.Busqueda = q; 

            var lista = await inventario.OrderBy(i => i.Producto.Nombre).ToListAsync();
            return View(lista);
        }

        // 🔹 DETALLE DE MOVIMIENTOS POR PRODUCTO
        public async Task<IActionResult> Movimientos(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();

            ViewBag.Producto = producto;
            var movimientos = await _context.MovInventarios
                .Where(m => m.IdProducto == id)
                .OrderByDescending(m => m.Fecha)
                .ToListAsync();

            return View(movimientos);
        }

        // 🔹 NUEVO AJUSTE (ENTRADA/SALIDA MANUAL)
        public async Task<IActionResult> Ajuste(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();

            ViewBag.Producto = producto;
            return View();
        }



        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Ajuste(int id, string tipoMovimiento, int cantidad, string? observacion)
        {
            var producto = await _context.Productos.FindAsync(id);
            var inventario = await _context.Inventarios.FirstOrDefaultAsync(i => i.IdProducto == id);

            if (producto == null || inventario == null)
                return NotFound();

            // Registrar movimiento
            var mov = new MovInventario
            {
                IdProducto = id,
                Fecha = DateTime.Now,
                TipoMovimiento = tipoMovimiento,
                Cantidad = cantidad,
                Observacion = observacion
            };
            _context.MovInventarios.Add(mov);

            // Actualizar stock
            if (tipoMovimiento == "Entrada")
                inventario.StockActual += cantidad;
            else if (tipoMovimiento == "Salida")
                inventario.StockActual -= cantidad;

            inventario.FechaUltimaActualizacion = DateTime.Now;
            _context.Update(inventario);

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Movimientos), new { id });
        }
    }
}
