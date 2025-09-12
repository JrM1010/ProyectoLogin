using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using System.Security.Claims;

namespace ProyectoLogin.Controllers
{
    [Authorize(Roles = "Administrador,Gerente")]
    public class ProductosController : Controller
    {
        private readonly DbpruebaContext _context;

        public ProductosController(DbpruebaContext context)
        {
            _context = context;
        }

        // GET: Productos/Create
        public async Task<IActionResult> Create()
        {
            await CargarViewData();
            return View("~/Views/Inventario/Create.cshtml");
        }

        // POST: Productos/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Producto producto)
        {
            if (ModelState.IsValid)
            {
                producto.FechaCreacion = DateTime.Now;
                producto.Activo = true;

                _context.Add(producto);
                await _context.SaveChangesAsync();

                // Registrar movimiento inicial
                await RegistrarMovimiento(producto.IdProducto, "ENTRADA", producto.Stock,
                    $"Creación del producto. Stock inicial: {producto.Stock}");

                return RedirectToAction(nameof(InventarioController.Index), "Inventario");
            }

            await CargarViewData();
            return View("~/Views/Inventario/Create.cshtml", producto); // Ruta completa con modelo
        }

        // GET: Productos/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound();
            }
            await CargarViewData();
            return View("~/Views/Inventario/Edit.cshtml", producto);
        }

        // POST: Productos/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Producto producto)
        {
            if (id != producto.IdProducto)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    // Mantener el stock original y fecha de creación
                    var productoExistente = await _context.Productos.FindAsync(id);
                    producto.Stock = productoExistente.Stock;
                    producto.FechaCreacion = productoExistente.FechaCreacion;
                    producto.Activo = productoExistente.Activo;

                    _context.Entry(productoExistente).CurrentValues.SetValues(producto);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ProductoExists(producto.IdProducto))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(InventarioController.Index), "Inventario");
            }
            await CargarViewData();
            return View("~/Views/Inventario/Edit.cshtml", producto);
        }

        // GET: Productos/Delete/5
        public async Task<IActionResult> Delete(int id)
        {
            var producto = await _context.Productos
         .Include(p => p.Categoria)
         .Include(p => p.Marca)
         .Include(p => p.Proveedor)
         .FirstOrDefaultAsync(p => p.IdProducto == id);

            if (producto == null)
            {
                return NotFound();
            }

            return View("~/Views/Inventario/Delete.cshtml", producto);
        }

        // POST: Productos/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            try
            {
                var producto = await _context.Productos.FindAsync(id);
                if (producto != null)
                {
                    // Verificar si hay movimientos de inventario recientes
                    var tieneMovimientosRecientes = await _context.MovimientosInventario
                        .AnyAsync(m => m.IdProducto == id && m.FechaMovimiento > DateTime.Now.AddDays(-30));

                    if (tieneMovimientosRecientes)
                    {
                        TempData["DeleteError"] = "No se puede eliminar el producto porque tiene movimientos de inventario recientes.";
                        return RedirectToAction(nameof(InventarioController.Index), "Inventario");
                    }

                    // Eliminación suave (soft delete)
                    producto.Activo = false;
                    _context.Update(producto);
                    await _context.SaveChangesAsync();

                    TempData["DeleteSuccess"] = $"Producto '{producto.Nombre}' eliminado correctamente.";
                }
            }
            catch (DbUpdateException ex)
            {
                TempData["DeleteError"] = "No se puede eliminar el producto porque está siendo utilizado en otras operaciones.";
            }
            catch (Exception ex)
            {
                TempData["DeleteError"] = "Ocurrió un error al eliminar el producto.";
            }

            return RedirectToAction(nameof(InventarioController.Index), "Inventario");
        }

        // GET: Productos/AjustarStock/5
        public async Task<IActionResult> AjustarStock(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound();
            }

            ViewBag.Producto = producto;
            return View("~/Views/Inventario/AjustarStock.cshtml", producto);
        }

        // POST: Productos/AjustarStock/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjustarStock(int id, int cantidad, string motivo, string tipoMovimiento)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null)
            {
                return NotFound();
            }

            var stockAnterior = producto.Stock;

            if (tipoMovimiento == "ENTRADA")
            {
                producto.Stock += cantidad;
            }
            else if (tipoMovimiento == "SALIDA")
            {
                if (cantidad > producto.Stock)
                {
                    ModelState.AddModelError("", "No puede retirar más unidades de las que existen en stock");
                    ViewBag.Producto = producto;
                    return View("~/Views/Inventario/AjustarStock.cshtml", producto);
                }
                producto.Stock = Math.Max(0, producto.Stock - cantidad);
            }
            else if (tipoMovimiento == "AJUSTE")
            {
                producto.Stock = cantidad;
            }

            _context.Update(producto);
            await _context.SaveChangesAsync();

            await RegistrarMovimiento(id, tipoMovimiento, cantidad, motivo, stockAnterior, producto.Stock);

            return RedirectToAction("Detalles", "Inventario", new { id = id });
        }

        private bool ProductoExists(int id)
        {
            return _context.Productos.Any(e => e.IdProducto == id);
        }

        private async Task CargarViewData()
        {
            ViewBag.Categorias = await _context.Categorias
                .Where(c => c.Activo)
                .Select(c => new SelectListItem { Value = c.IdCategoria.ToString(), Text = c.Nombre })
                .ToListAsync();

            ViewBag.Marcas = await _context.Marcas
                .Where(m => m.Activo)
                .Select(m => new SelectListItem { Value = m.IdMarca.ToString(), Text = m.Nombre })
                .ToListAsync();

            ViewBag.Proveedores = await _context.Proveedores
                .Where(p => p.Activo)
                .Select(p => new SelectListItem { Value = p.IdProveedor.ToString(), Text = p.Nombre })
                .ToListAsync();
        }






        private async Task RegistrarMovimiento(int idProducto, string tipoMovimiento, int cantidad,
            string motivo, int stockAnterior = 0, int stockNuevo = 0)
        {

            // Obtener el ID del usuario de forma segura
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);

            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                // Si no hay usuario autenticado, usar un valor por defecto o lanzar excepción
                // Dependiendo de tu lógica de negocio
                userId = 1; // O manejar el error apropiadamente
            }

            var movimiento = new MovimientoInventario
            {
                IdProducto = idProducto,
                IdUsuario = userId, // Usar el ID obtenido
                TipoMovimiento = tipoMovimiento,
                Cantidad = cantidad,
                StockAnterior = stockAnterior,
                StockNuevo = stockNuevo,
                Motivo = motivo,
                FechaMovimiento = DateTime.Now
            };

            _context.MovimientosInventario.Add(movimiento);
            await _context.SaveChangesAsync();
        }
    }
}
