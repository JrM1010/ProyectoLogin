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

            // 🟢 Productos normales
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
                    stock = p.Inventario != null ? p.Inventario.StockActual : 0,
                    tipo = "producto"  // ⚡️ nuevo campo
                })
                .Take(15)
                .ToListAsync();

            // 🟣 Promociones (Kits)
            var kits = await _context.Kits
                .Where(k => k.Activo && k.Nombre.Contains(term))
                .Select(k => new
                {
                    id = k.IdKit,
                    text = "(KIT) " + k.Nombre,
                    precio = k.Total,
                    stock = -1, // sin stock propio, se calcula por componentes
                    tipo = "kit"  // ⚡️ nuevo campo
                })
                .Take(10)
                .ToListAsync();

            var resultados = productos.Concat(kits).ToList();

            return Json(new { results = resultados });
        }


        // ==================================================
        // 3️⃣  Guardar venta (POST principal del formulario)
        // ==================================================
        [HttpPost]
        public async Task<IActionResult> GuardarVenta([FromBody] Venta venta)
        {
            if (venta == null || venta.Detalles == null || !venta.Detalles.Any(d =>
                    (d.IdProducto.HasValue || d.IdKit.HasValue) &&
                     d.Cantidad > 0 && d.PrecioUnitario > 0))
            {
                return BadRequest(new { success = false, message = "Datos de venta incompletos." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (userId == null)
                return Unauthorized(new { success = false, message = "No se pudo identificar el usuario." });

            venta.IdUsuario = int.Parse(userId);
            venta.FechaVenta = FechaLocal.Ahora();

            // Calcular totales
            venta.Subtotal = venta.Detalles.Sum(d => d.Subtotal);
            venta.IVA = venta.Subtotal * 0.12m;
            venta.Total = venta.Subtotal + venta.IVA;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // ========================================================
                // 🔹 1️⃣ Validar stock de productos y promociones (kits)
                // ========================================================
                foreach (var det in venta.Detalles)
                {
                    // 🟢 Producto normal
                    if (det.IdProducto > 0)
                    {
                        var inventario = await _context.Inventarios
                            .FirstOrDefaultAsync(i => i.IdProducto == det.IdProducto);

                        if (inventario == null)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new
                            {
                                success = false,
                                message = $"El producto con ID {det.IdProducto} no tiene inventario asociado."
                            });
                        }

                        if (inventario.StockActual < det.Cantidad)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Stock insuficiente para el producto '{det.IdProducto}'. " +
                                          $"Disponible: {inventario.StockActual}, solicitado: {det.Cantidad}."
                            });
                        }
                    }

                    // 🟣 Promoción (Kit)
                    else if (det.IdKit != null && det.IdKit > 0)
                    {
                        var kit = await _context.Kits
                            .Include(k => k.Detalles!)
                                .ThenInclude(d => d.Producto)
                            .FirstOrDefaultAsync(k => k.IdKit == det.IdKit);

                        if (kit == null)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new { success = false, message = "Promoción no encontrada." });
                        }

                        // Verificar stock de cada producto dentro del kit
                        foreach (var kd in kit.Detalles)
                        {
                            var inventario = await _context.Inventarios
                                .FirstOrDefaultAsync(i => i.IdProducto == kd.IdProducto);

                            if (inventario == null || inventario.StockActual < kd.Cantidad)
                            {
                                await transaction.RollbackAsync();
                                return BadRequest(new
                                {
                                    success = false,
                                    message = $"Stock insuficiente para '{kd.Producto?.Nombre ?? "producto"}' en promoción '{kit.Nombre}'."
                                });
                            }
                        }
                    }
                }

                // ========================================================
                // 🔹 2️⃣ Registrar venta
                // ========================================================
                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                // ========================================================
                // 🔹 3️⃣ Descontar inventario
                // ========================================================
                foreach (var det in venta.Detalles)
                {
                    // Producto normal
                    if (det.IdProducto > 0)
                    {
                        var inventario = await _context.Inventarios
                            .FirstOrDefaultAsync(i => i.IdProducto == det.IdProducto);

                        if (inventario != null)
                        {
                            inventario.StockActual -= det.Cantidad;
                            inventario.FechaUltimaActualizacion = FechaLocal.Ahora();
                            _context.Inventarios.Update(inventario);
                        }
                    }

                    // Promoción (Kit)
                    else if (det.IdKit != null && det.IdKit > 0)
                    {
                        var kit = await _context.Kits
                            .Include(k => k.Detalles!)
                            .FirstOrDefaultAsync(k => k.IdKit == det.IdKit);

                        if (kit != null)
                        {
                            foreach (var kd in kit.Detalles)
                            {
                                var inventario = await _context.Inventarios
                                    .FirstOrDefaultAsync(i => i.IdProducto == kd.IdProducto);

                                if (inventario != null)
                                {
                                    inventario.StockActual -= kd.Cantidad; // 👈 resta por producto del kit
                                    inventario.FechaUltimaActualizacion = FechaLocal.Ahora();
                                    _context.Inventarios.Update(inventario);
                                }
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Ok(new
                {
                    success = true,
                    message = "✅ Venta registrada correctamente.",
                    idVenta = venta.IdVenta
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new
                {
                    success = false,
                    message = "❌ Error al guardar venta: " + ex.Message
                });
            }
        }






        [HttpGet]
        public async Task<IActionResult> BuscarPromocion(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new { results = new List<object>() });

            var kits = await _context.Kits
                .Include(k => k.Detalles!)
                    .ThenInclude(d => d.Producto)
                .Where(k => k.Activo &&
                            (k.Nombre.Contains(term) || (k.Descripcion ?? "").Contains(term)))
                .Select(k => new
                {
                    id = k.IdKit,
                    text = k.Nombre,
                    total = k.Total,
                    descuento = k.DescuentoPct,
                    productos = k.Detalles.Select(d => new
                    {
                        idProducto = d.IdProducto,
                        cantidad = d.Cantidad,
                        precio = d.PrecioUnitarioSnapshot
                    })
                })
                .Take(10)
                .ToListAsync();

            return Json(new { results = kits });
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




        // 🔍 Buscar cliente por NIT (AJAX)
        [HttpGet]
        public async Task<IActionResult> BuscarClientePorNit(string nit)
        {
            if (string.IsNullOrWhiteSpace(nit))
                return Json(new { encontrado = false });

            string nitLimpio = new string(nit.Where(char.IsLetterOrDigit).ToArray()); // elimina guiones y espacios

            var cliente = await _context.Clientes
                .Where(c => c.Nit.Replace("-", "") == nitLimpio && c.Activo)
                            .Select(c => new
                {
                    c.IdCliente,
                    c.Nit,
                    c.Nombres,
                    c.Apellidos,
                    c.Correo,
                    c.Direccion
                })
                .FirstOrDefaultAsync();

            if (cliente == null)
                return Json(new { encontrado = false });

            return Json(new { encontrado = true, cliente });
        }

        // ➕ Registrar cliente rápido desde la venta
        [HttpPost]
        public async Task<IActionResult> RegistrarClienteRapido([FromBody] Cliente nuevo)
        {
            if (nuevo == null || string.IsNullOrWhiteSpace(nuevo.Nit))
                return BadRequest("Datos de cliente inválidos.");

            // 🔹 Limpiar el NIT (elimina guiones, espacios y caracteres especiales)
            nuevo.Nit = new string(nuevo.Nit.Where(char.IsLetterOrDigit).ToArray());

            // 🔹 Evitar duplicados (comparando sin guiones)
            bool existe = await _context.Clientes
                .AnyAsync(c => c.Nit.Replace("-", "") == nuevo.Nit);

            if (existe)
                return BadRequest("Ya existe un cliente con este NIT.");

            // 🔹 Completar datos del nuevo cliente
            nuevo.FechaCreacion = FechaLocal.Ahora();
            nuevo.Activo = true;

            _context.Clientes.Add(nuevo);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                IdCliente = nuevo.IdCliente,
                nuevo.Nit,
                nuevo.Nombres,
                nuevo.Apellidos,
                nuevo.Correo,
                nuevo.Direccion
            });
        }


    }
}
