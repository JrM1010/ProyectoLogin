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
            return File(pdfBytes, "application/pdf", $"Factura_{idVenta}.pdf");
        }








        // Vista principal del POS
        public IActionResult Index()
        {
            return View("~/Views/Ventas/Index.cshtml");
        }

        // Búsqueda rápida de producto (AJAX)
        // Dentro de VentasController
        [HttpGet]
        public async Task<IActionResult> BuscarProducto(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new { results = new List<object>() });

            // Traer unidades globales (proyectadas a un shape común)
            var unidadesGlobales = await _context.UnidadesMedida
                .Where(u => u.Activo)
                .Select(u => new
                {
                    IdUnidad = u.IdUnidad,
                    Nombre = u.Nombre,
                    FactorConversion = u.EquivalenciaEnUnidades
                })
                .ToListAsync();

            // Cargar productos básicos (cada producto traerá su lista de unidades proyectada)
            var productos = await _context.Productos
                .Include(p => p.Inventario)
                .Where(p => p.Activo && (p.Nombre.Contains(term) || p.CodigoBarras.Contains(term)))
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
                    tipo = "producto",
                    // Proyectar unidades del producto al mismo shape que las globales
                    unidades = _context.ProductosUnidades
                        .Where(pu => pu.IdProducto == p.IdProducto)
                        .Select(pu => new
                        {
                            IdUnidad = pu.IdUnidad,
                            Nombre = pu.UnidadMedida.Nombre,
                            FactorConversion = pu.FactorConversion
                        })
                        .ToList()
                })
                .Take(15)
                .ToListAsync();

            // Normalizar productos: si no tiene unidades específicas, usar las globales.
            var productosNormalized = productos.Select(p =>
            {
                // p.unidades es List<anon> (puede estar vacío). Queremos una List<object> con mismo shape.
                var unidadesProd = (p.unidades as IEnumerable<object>)?.Cast<object>().ToList();

                // Si no tiene unidades propias, usar las globales (convertidas a object)
                List<object> unidadesFinal;
                if (unidadesProd == null || !unidadesProd.Any())
                {
                    unidadesFinal = unidadesGlobales.Cast<object>().ToList();
                }
                else
                {
                    unidadesFinal = unidadesProd;
                }

                return new
                {
                    p.id,
                    p.text,
                    p.precio,
                    p.stock,
                    p.tipo,
                    unidades = unidadesFinal
                };
            }).ToList();

            // KITS (igual que antes)
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

            // Concatenar (ambos son listas de objetos anónimos; el serializador JSON los manejará)
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

            // Calcular totales
            venta.Subtotal = venta.Detalles.Sum(d => d.Subtotal);
            venta.IVA = venta.Subtotal * 0.12m;
            venta.Total = venta.Subtotal + venta.IVA;

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

                        // ✅ Obtener factor de conversión según unidad
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

                        // 🔹 Calcular cantidad real en unidades base
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

                // === REGISTRAR VENTA ===
                _context.Ventas.Add(venta);
                await _context.SaveChangesAsync();

                // === DESCONTAR INVENTARIO ===
                foreach (var det in venta.Detalles)
                {
                    // 🧩 PRODUCTO NORMAL
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

                            // 🔹 Cantidad real en unidades base
                            var cantidadReal = det.Cantidad * factor;

                            inventario.StockActual -= cantidadReal;
                            inventario.FechaUltimaActualizacion = FechaLocal.Ahora();
                            _context.Inventarios.Update(inventario);
                        }
                    }

                    // 🧩 PROMOCIÓN (KIT)
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
                                    // Cada kit descuenta sus productos base
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
