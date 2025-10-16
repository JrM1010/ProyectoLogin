using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosProducts;
using ProyectoLogin.Models.ModelosVentas;
using ProyectoLogin.Recursos;
using System.Security.Claims;

namespace ProyectoLogin.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor")]
    public class VentasController : Controller
    {
        private readonly DbPruebaContext _context;

        public VentasController(DbPruebaContext context)
        {
            _context = context;
        }

        // =============================
        // 1️⃣  Vista principal del POS
        // =============================
        public IActionResult Index()
        {
            return View("~/Views/Ventas/Index.cshtml");
        }

        // ============================================
        // 2️⃣  Búsqueda rápida de producto (AJAX)
        // ============================================
        [HttpGet]
        public async Task<IActionResult> BuscarProducto(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new { results = new List<object>() });

            var productos = await _context.Productos
                .Include(p => p.Inventario)
                .Where(p => p.Activo &&
                            (p.Nombre.Contains(term) || p.CodigoBarras.Contains(term)))
                .Select(p => new
                {
                    id = p.IdProducto,
                    text = p.Nombre,
                    precio = _context.ProductoPrecio
                        .Where(pr => pr.IdProducto == p.IdProducto && pr.Activo)
                        .OrderByDescending(pr => pr.FechaInicio)
                        .Select(pr => pr.PrecioVenta)
                        .FirstOrDefault(),
                    stock = p.Inventario != null ? p.Inventario.StockActual : 0
                })
                .Take(15)
                .ToListAsync();

            return Json(new { results = productos });
        }

        // ==================================================
        // 3️⃣  Guardar venta (POST principal del formulario)
        // ==================================================
        [HttpPost]
        public async Task<IActionResult> GuardarVenta([FromBody] Venta venta)
        {
            if (venta == null || venta.Detalles == null || !venta.Detalles.Any())
                return BadRequest("Datos de venta incompletos.");

            // Asignar usuario logueado
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized("No se pudo identificar el usuario.");

            venta.IdUsuario = int.Parse(userId);
            venta.FechaVenta = FechaLocal.Ahora();

            // Calcular totales
            venta.Subtotal = venta.Detalles.Sum(d => d.Subtotal);
            venta.IVA = venta.Subtotal * 0.12m;
            venta.Total = venta.Subtotal + venta.IVA;

            // Iniciar transacción
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Guardar encabezado
                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                foreach (var det in venta.Detalles)
                {
                    det.IdVenta = venta.IdVenta;

                    // Descontar del inventario
                    var inventario = await _context.Inventarios
                        .FirstOrDefaultAsync(i => i.IdProducto == det.IdProducto);

                    if (inventario == null)
                    {
                        throw new InvalidOperationException($"El producto con ID {det.IdProducto} no tiene inventario asociado.");
                    }

                    if (inventario.StockActual < det.Cantidad)
                    {
                        throw new InvalidOperationException($"Stock insuficiente para el producto {det.IdProducto}.");
                    }

                    inventario.StockActual -= det.Cantidad;
                    inventario.FechaUltimaActualizacion = FechaLocal.Ahora();

                    // Registrar movimiento
                    var mov = new MovInventario
                    {
                        IdProducto = det.IdProducto,
                        Cantidad = det.Cantidad,
                        Fecha = FechaLocal.Ahora(),
                        TipoMovimiento = "Venta",
                        Referencia = $"Venta #{venta.IdVenta}"
                    };

                    _context.MovInventarios.Add(mov);
                    _context.DetallesVenta.Add(det);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Venta registrada correctamente.", idVenta = venta.IdVenta });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Error al guardar venta: " + ex.Message });
            }
        }

        // =====================================
        // 4️⃣  Detalle de venta (vista simple)
        // =====================================
        public async Task<IActionResult> Detalle(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.Usuario)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.IdVenta == id);

            if (venta == null)
                return NotFound();

            return View("~/Views/Ventas/Detalle.cshtml", venta);
        }

        // =============================================
        // 5️⃣  Anular venta (solo administrador)
        // =============================================
        [Authorize(Roles = "Administrador")]
        [HttpPost]
        public async Task<IActionResult> Anular(int id)
        {
            var venta = await _context.Ventas
                .Include(v => v.Detalles)
                .FirstOrDefaultAsync(v => v.IdVenta == id);

            if (venta == null)
                return NotFound();

            if (venta.Estado == "Anulada")
                return BadRequest("La venta ya está anulada.");

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                venta.Estado = "Anulada";

                // Revertir stock
                foreach (var det in venta.Detalles)
                {
                    var inventario = await _context.Inventarios
                        .FirstOrDefaultAsync(i => i.IdProducto == det.IdProducto);

                    if (inventario != null)
                    {
                        inventario.StockActual += det.Cantidad;
                        inventario.FechaUltimaActualizacion = FechaLocal.Ahora();
                        _context.Inventarios.Update(inventario);
                    }

                    // Registrar movimiento
                    _context.MovInventarios.Add(new MovInventario
                    {
                        IdProducto = det.IdProducto,
                        Cantidad = det.Cantidad,
                        Fecha = FechaLocal.Ahora(),
                        TipoMovimiento = "Anulación Venta",
                        Referencia = $"Anulación #{venta.IdVenta}"
                    });
                }

                _context.Update(venta);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new { success = true, message = "Venta anulada correctamente." });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Error al anular venta: " + ex.Message });
            }
        }

        // =============================================
        // 6️⃣  Listado de ventas (para reporte/corte)
        // =============================================
        public async Task<IActionResult> Lista()
        {
            var ventas = await _context.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.Cliente)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();

            return View("~/Views/Ventas/Lista.cshtml", ventas);
        }
    }
}
