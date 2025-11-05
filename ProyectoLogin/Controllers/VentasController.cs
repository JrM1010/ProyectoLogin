using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosProducts;
using ProyectoLogin.Models.ModelosVentas;
using ProyectoLogin.Recursos;
using ProyectoLogin.Servicios.Implementacion;
using System.Security.Claims;

namespace ProyectoLogin.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor")]
    public class VentasController : Controller
    {
        private readonly DbPruebaContext _context;
        private readonly FacturaService _facturaService;

        public VentasController(DbPruebaContext context, FacturaService facturaService)
        {
            _context = context;
            _facturaService = facturaService;
        }

        [HttpGet]
        public async Task<IActionResult> DescargarFactura(int idVenta)
        {
            var pdfBytes = await _facturaService.GenerarFacturaAsync(idVenta);
            var fechaActual = FechaLocal.Ahora().ToString("dd/MM/yyyy");
            var nombreArchivo = $"FacturaSmartcell {fechaActual}.pdf";

            return File(pdfBytes, "application/pdf", nombreArchivo);
        }

        // Vista principal del POS
        public IActionResult Index()
        {
            return View("~/Views/Ventas/Index.cshtml");
        }

        // 🔹 MÉTODO BUSCAR PRODUCTO - CORREGIDO Y COMPLETO
        [HttpGet]
        public async Task<IActionResult> BuscarProducto(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new { results = new List<object>() });

            // Traer unidades globales
            var unidadesGlobales = await _context.UnidadesMedida
                .Where(u => u.Activo)
                .Select(u => new
                {
                    IdUnidad = u.IdUnidad,
                    Nombre = u.Nombre,
                    FactorConversion = u.EquivalenciaEnUnidades,
                    MargenGanancia = u.MargenGanancia,
                    DescuentoAplicable = u.DescuentoAplicable
                })
                .ToListAsync();

            // Cargar productos con sus precios activos
            var productos = await _context.Productos
                .Include(p => p.Inventario)
                .Where(p => p.Activo && (p.Nombre.Contains(term) || p.CodigoBarras.Contains(term)))
                .Select(p => new
                {
                    id = p.IdProducto,
                    text = p.Nombre,
                    stock = p.Inventario != null ? p.Inventario.StockActual : 0,
                    tipo = "producto",
                    // Obtener el precio activo más reciente
                    precioActivo = _context.ProductoPrecio
                        .Where(pr => pr.IdProducto == p.IdProducto && pr.Activo)
                        .OrderByDescending(pr => pr.FechaInicio)
                        .FirstOrDefault()
                })
                .Take(15)
                .ToListAsync();

            // Normalizar productos con precios por presentación
            var productosNormalized = productos.Select(p =>
            {
                var precioActivo = p.precioActivo;

                // 🔹 CREAR PRECIOS POR UNIDAD BASADOS EN LOS PRECIOS ALMACENADOS
                var unidadesFinal = unidadesGlobales.Select(u => {
                    decimal precioVenta = 0;

                    // Asignar precio según la unidad
                    if (u.FactorConversion == 1)
                        precioVenta = precioActivo?.PrecioVentaUnidad ?? 0;
                    else if (u.FactorConversion == 6)
                        precioVenta = precioActivo?.PrecioVentaPaquete ?? 0;
                    else if (u.FactorConversion == 12)
                        precioVenta = precioActivo?.PrecioVentaCaja ?? 0;
                    else
                        precioVenta = precioActivo?.PrecioVenta ?? 0; // Fallback

                    return new
                    {
                        u.IdUnidad,
                        u.Nombre,
                        u.FactorConversion,
                        u.MargenGanancia,
                        u.DescuentoAplicable,
                        PrecioVenta = precioVenta
                    };
                }).Cast<object>().ToList();

                // Precio por defecto (unidad individual)
                var precioDefault = precioActivo?.PrecioVentaUnidad ?? precioActivo?.PrecioVenta ?? 0;

                return new
                {
                    p.id,
                    p.text,
                    precio = precioDefault,
                    preciosPorUnidad = unidadesFinal,
                    p.stock,
                    p.tipo,
                    unidades = unidadesFinal
                };
            }).ToList();

            // KITS (sin cambios)
            var kits = await _context.Kits
                .Where(k => k.Activo && k.Nombre.Contains(term))
                .Select(k => new
                {
                    id = k.IdKit,
                    text = "(KIT) " + k.Nombre,
                    precio = k.Total,
                    stock = -1,
                    tipo = "kit"
                })
                .Take(10)
                .ToListAsync();

            var resultados = productosNormalized.Concat(kits.Cast<object>()).ToList();

            return Json(new { results = resultados });
        }

        // Guardar venta
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

            // === Calcular totales (sin IVA) ===
            venta.Subtotal = venta.Detalles.Sum(d => d.Subtotal);
            venta.IVA = 0; // IVA siempre en 0
            venta.Total = venta.Subtotal; // Total igual al subtotal (precios ya incluyen IVA)

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                // === VALIDAR STOCK DE PRODUCTOS Y PROMOCIONES ===
                foreach (var det in venta.Detalles)
                {
                    // 🧩 PRODUCTO NORMAL
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

                        decimal factor = 1m;

                        if (det.IdUnidad.HasValue)
                        {
                            var productoUnidad = await _context.ProductosUnidades
                                .FirstOrDefaultAsync(pu => pu.IdProducto == det.IdProducto && pu.IdUnidad == det.IdUnidad);

                            if (productoUnidad != null)
                                factor = productoUnidad.FactorConversion;
                            else
                            {
                                var unidad = await _context.UnidadesMedida
                                    .FirstOrDefaultAsync(u => u.IdUnidad == det.IdUnidad);
                                if (unidad != null)
                                    factor = unidad.EquivalenciaEnUnidades;
                            }
                        }

                        var cantidadReal = det.Cantidad * factor;

                        if (inventario.StockActual < cantidadReal)
                        {
                            await transaction.RollbackAsync();
                            return BadRequest(new
                            {
                                success = false,
                                message = $"Stock insuficiente para el producto con ID {det.IdProducto}. " +
                                          $"Disponible: {inventario.StockActual}, solicitado: {cantidadReal} unidades base."
                            });
                        }
                    }
                    // 🧩 PROMOCIÓN (KIT)
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
                                    message = $"Stock insuficiente para '{kd.Producto?.Nombre ?? "producto"}' en la promoción '{kit.Nombre}'."
                                });
                            }
                        }
                    }
                }

                // === GENERAR NÚMEROS DE VENTA Y FACTURA ===
                int totalVentas = await _context.Ventas.CountAsync();
                venta.NumeroVenta = $"V-{(totalVentas + 1).ToString("D6")}";
                venta.NumeroFactura = $"F-{FechaLocal.Ahora():yyyy}-{(totalVentas + 1).ToString("D6")}";

                // === REGISTRAR VENTA ===
                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                // === CALCULAR UTILIDAD DE LA VENTA ===
                decimal utilidadTotalVenta = 0m;

                // obtener lista de productos vendidos
                var productoIds = venta.Detalles
                    .Where(d => d.IdProducto.HasValue)
                    .Select(d => d.IdProducto!.Value)
                    .Distinct()
                    .ToList();

                // obtener precios base activos (sin IVA)
                var preciosBase = await _context.ProductoPrecio
                    .Where(pp => productoIds.Contains(pp.IdProducto) && pp.Activo)
                    .GroupBy(pp => pp.IdProducto)
                    .Select(g => g.OrderByDescending(x => x.FechaInicio).FirstOrDefault())
                    .ToDictionaryAsync(x => x.IdProducto, x => x.PrecioBase);

                foreach (var det in venta.Detalles)
                {
                    decimal utilidad = 0m;

                    // 🧩 Producto normal
                    if (det.IdProducto.HasValue)
                    {
                        decimal precioVentaConIVA = det.PrecioUnitario;
                        decimal precioVentaSinIVA = precioVentaConIVA / 1.12m;

                        decimal precioBase = preciosBase.ContainsKey(det.IdProducto.Value)
                            ? preciosBase[det.IdProducto.Value]
                            : 0m;

                        decimal factor = 1m;

                        if (det.IdUnidad.HasValue)
                        {
                            var productoUnidad = await _context.ProductosUnidades
                                .FirstOrDefaultAsync(pu => pu.IdProducto == det.IdProducto && pu.IdUnidad == det.IdUnidad);

                            if (productoUnidad != null)
                                factor = productoUnidad.FactorConversion;
                            else
                            {
                                var unidad = await _context.UnidadesMedida
                                    .FirstOrDefaultAsync(u => u.IdUnidad == det.IdUnidad);
                                if (unidad != null)
                                    factor = unidad.EquivalenciaEnUnidades;
                            }
                        }

                        // cantidad real en unidades base
                        var cantidadReal = det.Cantidad * factor;

                        utilidad = (precioVentaSinIVA - precioBase) * cantidadReal;
                    }

                    // 🧩 Kit / Promoción
                    else if (det.IdKit.HasValue)
                    {
                        var kit = await _context.Kits
                            .Include(k => k.Detalles!)
                            .ThenInclude(d => d.Producto)
                            .FirstOrDefaultAsync(k => k.IdKit == det.IdKit);

                        if (kit != null)
                        {
                            decimal utilidadKit = 0m;

                            foreach (var kd in kit.Detalles)
                            {
                                var precioBase = await _context.ProductoPrecio
                                    .Where(pp => pp.IdProducto == kd.IdProducto && pp.Activo)
                                    .OrderByDescending(pp => pp.FechaInicio)
                                    .Select(pp => pp.PrecioBase)
                                    .FirstOrDefaultAsync();

                                var precioVentaSinIVA = kd.PrecioUnitarioSnapshot / 1.12m;
                                utilidadKit += (precioVentaSinIVA - precioBase) * kd.Cantidad;
                            }

                            utilidad = utilidadKit * det.Cantidad;
                        }
                    }

                    det.Utilidad = Math.Round(utilidad, 2);
                    utilidadTotalVenta += det.Utilidad;
                }

                // === DESCONTAR INVENTARIO ===
                foreach (var det in venta.Detalles)
                {
                    if (det.IdProducto > 0)
                    {
                        var inventario = await _context.Inventarios
                            .FirstOrDefaultAsync(i => i.IdProducto == det.IdProducto);

                        if (inventario != null)
                        {
                            int factor = 1;

                            if (det.IdUnidad.HasValue)
                            {
                                var productoUnidad = await _context.ProductosUnidades
                                    .FirstOrDefaultAsync(pu => pu.IdProducto == det.IdProducto && pu.IdUnidad == det.IdUnidad);

                                if (productoUnidad != null)
                                    factor = productoUnidad.FactorConversion;
                                else
                                {
                                    var unidad = await _context.UnidadesMedida
                                        .FirstOrDefaultAsync(u => u.IdUnidad == det.IdUnidad);
                                    if (unidad != null)
                                        factor = unidad.EquivalenciaEnUnidades;
                                }
                            }

                            var cantidadReal = det.Cantidad * factor;

                            inventario.StockActual -= cantidadReal;
                            inventario.FechaUltimaActualizacion = FechaLocal.Ahora();
                            _context.Inventarios.Update(inventario);
                        }
                    }
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
                                    inventario.StockActual -= kd.Cantidad;
                                    inventario.FechaUltimaActualizacion = FechaLocal.Ahora();
                                    _context.Inventarios.Update(inventario);
                                }
                            }
                        }
                    }
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 🔹 Registrar movimiento en bitácora
                var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = idUsuario,
                    Accion = "Registro de venta",
                    Descripcion = $"Venta #{venta.NumeroVenta} realizada. Total: Q{venta.Total:F2}",
                    Modulo = "Ventas",
                    Fecha = FechaLocal.Ahora()
                });

                await _context.SaveChangesAsync();

                return Ok(new
                {
                    success = true,
                    message = "✅ Venta registrada correctamente.",
                    idVenta = venta.IdVenta,
                    numeroVenta = venta.NumeroVenta,
                    numeroFactura = venta.NumeroFactura
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

        // Detalle de venta 
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

        // Listado de ventas 
        public async Task<IActionResult> Lista()
        {
            var ventas = await _context.Ventas
                .Include(v => v.Usuario)
                .Include(v => v.Cliente)
                .OrderByDescending(v => v.FechaVenta)
                .ToListAsync();

            return View("~/Views/Ventas/Lista.cshtml", ventas);
        }

        // Buscar cliente por NIT 
        [HttpGet]
        public async Task<IActionResult> BuscarClientePorNit(string nit)
        {
            if (string.IsNullOrWhiteSpace(nit))
                return Json(new { encontrado = false });

            string nitLimpio = new string(nit.Where(char.IsLetterOrDigit).ToArray());

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

        // Registrar cliente rápido desde la venta
        [HttpPost]
        public async Task<IActionResult> RegistrarClienteRapido([FromBody] Cliente nuevo)
        {
            if (nuevo == null || string.IsNullOrWhiteSpace(nuevo.Nit))
                return BadRequest("Datos de cliente inválidos.");

            // Limpiar el NIT
            nuevo.Nit = new string(nuevo.Nit.Where(char.IsLetterOrDigit).ToArray());

            // Evitar duplicados (comparando sin guiones)
            bool existe = await _context.Clientes
                .AnyAsync(c => c.Nit.Replace("-", "") == nuevo.Nit);

            if (existe)
                return BadRequest("Ya existe un cliente con este NIT.");

            // Completar datos del nuevo cliente
            nuevo.FechaCreacion = FechaLocal.Ahora();
            nuevo.Activo = true;

            _context.Clientes.Add(nuevo);
            await _context.SaveChangesAsync();

            // 🔹 Registrar movimiento en bitácora
            var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

            _context.BitacoraMovimientos.Add(new BitacoraMovimiento
            {
                IdUsuario = idUsuario,
                Accion = "Registro de cliente rápido",
                Descripcion = $"Cliente {nuevo.Nombres} {nuevo.Apellidos} (NIT: {nuevo.Nit}) agregado desde el módulo de ventas.",
                Modulo = "Ventas",
                Fecha = FechaLocal.Ahora()
            });

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