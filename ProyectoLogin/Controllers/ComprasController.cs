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
        public ComprasController(DbPruebaContext context) { _context = context; }

        // Index - historial de compras
        public async Task<IActionResult> Index()
        {
            var compras = await _context.Compras
                .Include(c => c.Proveedor)
                .OrderByDescending(c => c.FechaCompra)
                .ToListAsync();
            return View(compras);
        }

        // GET: Create
        public IActionResult Create()
        {
            ViewBag.Proveedores = _context.Proveedores.Where(p => p.Activo).ToList();
            ViewBag.Productos = _context.Productos.Where(p => p.Activo).ToList();
            ViewBag.Unidades = _context.Unidades.ToList();
            return View(new Compra { FechaCompra = DateTime.Now });
        }

        // POST: Create (recibe compra y lista de detalles)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Compra compra, List<DetalleCompra> detalles)
        {
            if (detalles == null || detalles.Count == 0)
            {
                ModelState.AddModelError("", "Debe agregar al menos un detalle.");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Proveedores = _context.Proveedores.Where(p => p.Activo).ToList();
                ViewBag.Productos = _context.Productos.Where(p => p.Activo).ToList();
                ViewBag.Unidades = _context.Unidades.ToList();
                return View(compra);
            }

            // Calcular Subtotales, aplicar descuentos por unidad si aplica
            decimal subtotal = 0m;
            foreach (var d in detalles)
            {
                var unidad = await _context.Unidades.FindAsync(d.IdUnidad);
                decimal descuentoPct = unidad?.DescuentoPorcentual ?? 0m;

                // Aplicar descuento por unidad al precio unitario (ejemplo)
                var precioConDescuento = d.PrecioUnitario * (1 - (descuentoPct / 100m));
                d.Subtotal = Math.Round(precioConDescuento * d.Cantidad, 2);
                subtotal += d.Subtotal;
            }

            decimal iva = Math.Round(subtotal * 0.12m, 2); // ejemplo: 12% IVA (ajusta si tu fiscal es distinto)
            compra.Subtotal = subtotal;
            compra.IVA = iva;
            compra.Total = subtotal + iva;

            using var trx = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Compras.Add(compra);
                await _context.SaveChangesAsync();

                // Guardar detalles y actualizar inventario + registro de movimientos
                foreach (var d in detalles)
                {
                    d.IdCompra = compra.IdCompra;
                    _context.DetallesCompra.Add(d);

                    // Actualizar inventario: suma la cantidad (entrada)
                    var inv = await _context.Inventarios.FirstOrDefaultAsync(i => i.IdProducto == d.IdProducto);
                    if (inv == null)
                    {
                        inv = new ProyectoLogin.Models.ModelosProducts.Inventario
                        {
                            IdProducto = d.IdProducto,
                            StockActual = 0,
                            StockMinimo = 0
                        };
                        _context.Inventarios.Add(inv);
                        await _context.SaveChangesAsync();
                    }

                    // Considerar factor de conversión (si tu unidad representa más de 1 unidad base)
                    var unidad = await _context.Unidades.FindAsync(d.IdUnidad);
                    decimal factor = unidad?.FactorConversion ?? 1m;
                    int cantidadEnUnidades = (int)Math.Round(d.Cantidad * factor); // convertir a unidades enteras si así lo manejas

                    inv.StockActual += cantidadEnUnidades;
                    inv.FechaUltimaActualizacion = DateTime.Now;
                    _context.Inventarios.Update(inv);

                    // Crear movimiento
                    _context.MovInventarios.Add(new ProyectoLogin.Models.ModelosProducts.MovInventario
                    {
                        IdProducto = d.IdProducto,
                        Cantidad = d.Cantidad,
                        Fecha = DateTime.Now,
                        TipoMovimiento = "Entrada - Compra",
                        Referencia = compra.NumeroDocumento,
                        Observacion = $"Compra Id {compra.IdCompra} Proveedor {compra.IdProveedor}"
                    });
                }

                await _context.SaveChangesAsync();
                await trx.CommitAsync();

                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                await trx.RollbackAsync();
                ModelState.AddModelError("", "Ocurrió un error guardando la compra: " + ex.Message);
                ViewBag.Proveedores = _context.Proveedores.Where(p => p.Activo).ToList();
                ViewBag.Productos = _context.Productos.Where(p => p.Activo).ToList();
                ViewBag.Unidades = _context.Unidades.ToList();
                return View(compra);
            }
        }

        // Details
        public async Task<IActionResult> Details(int id)
        {
            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Unidad)
                .FirstOrDefaultAsync(c => c.IdCompra == id);

            if (compra == null) return NotFound();
            return View(compra);
        }
    }
}
