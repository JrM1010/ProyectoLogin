using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosCompras;
using ProyectoLogin.Models.ModelosProducts;

namespace ProyectoLogin.Controllers
{
    public class ComprasController : Controller
    {
        private readonly DbPruebaContext _context;

        public ComprasController(DbPruebaContext context)
        {
            _context = context;
        }

        // LISTAR COMPRAS
        public async Task<IActionResult> Index()
        {
            var compras = await _context.Compras
                .Include(c => c.Proveedor)
                .OrderByDescending(c => c.FechaCompra)
                .ToListAsync();
            return View(compras);
        }

        // NUEVA COMPRA - GET
        public IActionResult Create()
        {
            ViewBag.Proveedores = _context.Proveedores.Where(p => p.Activo).ToList();
            ViewBag.Productos = _context.Productos.Where(p => p.Activo).ToList();
            ViewBag.Unidades = _context.Unidades.ToList();
            return View();
        }

        // NUEVA COMPRA - POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Compra compra, List<DetalleCompra> detalles)
        {
            if (detalles == null || detalles.Count == 0)
                return BadRequest("Debe agregar al menos un producto.");

            compra.Subtotal = detalles.Sum(d => d.Subtotal);
            compra.Total = compra.Subtotal + (compra.IVA ?? 0);

            _context.Compras.Add(compra);
            await _context.SaveChangesAsync();

            foreach (var item in detalles)
            {
                item.IdCompra = compra.IdCompra;
                _context.DetallesCompra.Add(item);

                // Actualizar stock
                var inventario = await _context.Inventarios.FirstOrDefaultAsync(i => i.IdProducto == item.IdProducto);
                if (inventario != null)
                    inventario.StockActual += (int)item.Cantidad;

                // Registrar movimiento
                _context.MovInventarios.Add(new MovInventario
                {
                    IdProducto = item.IdProducto,
                    Fecha = DateTime.Now,
                    Cantidad = item.Cantidad,
                    TipoMovimiento = "Entrada por compra",
                    Referencia = compra.NumeroDocumento,
                    Observacion = "Compra registrada"
                });
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
