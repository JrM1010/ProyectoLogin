using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosCompras;
using ProyectoLogin.Models.ModelosProducts;
using ProyectoLogin.Models.UnidadesDeMedida;
using ProyectoLogin.Recursos;
using System;

namespace ProyectoLogin.Controllers
{
    [Authorize(Roles = "Administrador,Gerente")]
    public class ComprasController : Controller
    {
        private readonly DbPruebaContext _context;

        public ComprasController(DbPruebaContext context)
        {
            _context = context;
        }

        // 🔹 LISTAR COMPRAS CON FILTROS Y PAGINACIÓN
        public async Task<IActionResult> Index(string estado = "Pendiente", string proveedor = "", int pagina = 1)
        {
            Response.Headers["Cache-Control"] = "no-cache, no-store, must-revalidate";
            Response.Headers["Pragma"] = "no-cache";
            Response.Headers["Expires"] = "0";

            int elementosPorPagina = 10;
            int elementosASaltar = (pagina - 1) * elementosPorPagina;

            var consulta = _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.UnidadMedida)
                .Where(c => c.Estado == estado)
                .OrderByDescending(c => c.FechaCompra)
                .AsSplitQuery()
                .Select(c => new
                {
                    Compra = c,
                    TotalCalculado = c.Detalles.Sum(d =>
                        (d.Cantidad * (d.PrecioUnitario / 1.12m) *
                         d.UnidadMedida.EquivalenciaEnUnidades) * 1.12m
                    )
                });

            if (!string.IsNullOrEmpty(proveedor))
            {
                consulta = consulta.Where(c => c.Compra.Proveedor.Nombre.Contains(proveedor));
            }

            int totalElementos = await consulta.CountAsync();
            int totalPaginas = (int)Math.Ceiling(totalElementos / (double)elementosPorPagina);

            var comprasConTotales = await consulta
                .Skip(elementosASaltar)
                .Take(elementosPorPagina)
                .ToListAsync();

            var compras = comprasConTotales.Select(c =>
            {
                if (Math.Abs(c.TotalCalculado - c.Compra.Total) > 0.01m)
                {
                    c.Compra.Total = c.TotalCalculado;
                }
                return c.Compra;
            }).ToList();

            ViewBag.EstadoActual = estado;
            ViewBag.Estados = new List<string> { "Pendiente", "Confirmada", "Completada" };
            ViewBag.ProveedorFiltro = proveedor;
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalElementos = totalElementos;
            ViewBag.ElementosPorPagina = elementosPorPagina;

            ViewBag.Proveedores = await _context.Proveedores
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombre)
                .Select(p => new { p.IdProveedor, p.Nombre })
                .ToListAsync();

            return View("~/Views/Compras/Index.cshtml", compras);
        }



        // 🔹 CREAR COMPRA (GET)
        public async Task<IActionResult> Create(int? idProveedor)
        {
            await CargarDatosVista(idProveedor);

            // Primera carga: sin proveedor seleccionado
            if (!idProveedor.HasValue)
            {
                return View(new Compra());
            }

            int proveedorId = idProveedor.Value;

            // Cargar productos del proveedor
            var productosProveedor = await _context.ProductosProveedores
                .Include(pp => pp.Producto)
                    .ThenInclude(p => p.ProductosUnidades)
                        .ThenInclude(pu => pu.UnidadMedida)
                .Where(pp => pp.IdProveedor == proveedorId)
                .Select(pp => new
                {
                    pp.Producto.IdProducto,
                    pp.Producto.Nombre,
                    Unidades = pp.Producto.ProductosUnidades.Any()
                        ? pp.Producto.ProductosUnidades.Select(pu => new
                        {
                            pu.IdUnidad,
                            Nombre = pu.UnidadMedida.Nombre,
                            pu.FactorConversion
                        })
                        : _context.UnidadesMedida
                            .Where(u => u.Activo)
                            .Select(u => new
                            {
                                u.IdUnidad,
                                Nombre = u.Nombre,
                                FactorConversion = u.EquivalenciaEnUnidades
                            })
                })
                .ToListAsync();

            // Seguridad extra: si por URL forzan un proveedor sin productos, lo rechazo
            if (!productosProveedor.Any())
            {
                TempData["Error"] = "El proveedor seleccionado no tiene productos asociados.";
                return RedirectToAction(nameof(Create));
            }

            ViewBag.Productos = productosProveedor;
            ViewBag.ProveedorSeleccionado = proveedorId;

            // Generar código de compra
            var random = new Random();
            var numeroDocumento = random.Next(100000000, 999999999).ToString();
            ViewBag.NumeroDocumento = numeroDocumento;

            return View(new Compra
            {
                IdProveedor = proveedorId,
                Estado = "Pendiente",
                NumeroDocumento = numeroDocumento
            });
        }



        // 🔹 CREAR COMPRA (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Compra compra, List<DetalleCompra> detalles)
        {
            detalles = detalles
                .Where(d => d.IdProducto > 0 && d.Cantidad > 0 && d.PrecioUnitario > 0)
                .ToList();

            if (!ValidarCompra(compra, detalles))
            {
                var msg = "Debe seleccionar un proveedor válido con productos asociados y agregar al menos un producto.";

                if (EsAjax())
                {
                    return Json(new { success = false, message = msg });
                }

                TempData["Error"] = msg;
                await CargarDatosVista(compra.IdProveedor);
                return View(compra);
            }

            compra.Estado = "Pendiente";
            compra.FechaCompra = FechaLocal.Ahora();
            compra.Detalles = null;

            await CalcularTotalesAsync(compra, detalles);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                compra.IdUsuario = idUsuario;

                _context.Compras.Add(compra);
                await _context.SaveChangesAsync();

                foreach (var det in detalles)
                {
                    det.IdCompra = compra.IdCompra;
                    _context.DetallesCompra.Add(det);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = idUsuario,
                    Accion = "Registro de compra",
                    Descripcion = $"Compra #{compra.IdCompra} creada.",
                    Modulo = "Compras",
                    Fecha = FechaLocal.Ahora()
                });
                await _context.SaveChangesAsync();

                if (EsAjax())
                {
                    return Json(new
                    {
                        success = true,
                        redirectUrl = Url.Action(nameof(Index), new { estado = "Pendiente" })
                    });
                }

                TempData["Success"] = "Compra creada correctamente.";
                return RedirectToAction(nameof(Index), new { estado = "Pendiente" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                var msg = "Error al registrar la compra: " + ex.Message;

                if (EsAjax())
                {
                    return Json(new { success = false, message = msg });
                }

                TempData["Error"] = msg;
                await CargarDatosVista(compra.IdProveedor);
                return View(compra);
            }
        }

        private bool EsAjax()
        {
            return Request.Headers["X-Requested-With"] == "XMLHttpRequest";
        }


        // 🔹 DETALLES DE COMPRA
        public async Task<IActionResult> Details(int id)
        {
            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.UnidadMedida)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.IdCompra == id);

            if (compra == null) return NotFound();
            return View(compra);
        }

        // 🔹 EDITAR COMPRA (GET)
        public async Task<IActionResult> Edit(int id)
        {
            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .AsSplitQuery()
                .FirstOrDefaultAsync(c => c.IdCompra == id);

            if (compra == null)
                return NotFound();

            if (compra.Estado != "Pendiente")
            {
                TempData["Error"] = "Solo se pueden editar compras en estado Pendiente.";
                return RedirectToAction(nameof(Index), new { estado = compra.Estado });
            }

            await CargarDatosVista(compra.IdProveedor);

            var productosProveedor = await _context.ProductosProveedores
                .Include(pp => pp.Producto)
                .Where(pp => pp.IdProveedor == compra.IdProveedor)
                .Select(pp => new
                {
                    pp.Producto.IdProducto,
                    pp.Producto.Nombre,
                    Unidades = pp.Producto.ProductosUnidades.Any()
                        ? pp.Producto.ProductosUnidades.Select(pu => new
                        {
                            pu.IdUnidad,
                            Nombre = pu.UnidadMedida.Nombre,
                            pu.FactorConversion
                        })
                        : _context.UnidadesMedida
                            .Where(u => u.Activo)
                            .Select(u => new
                            {
                                u.IdUnidad,
                                Nombre = u.Nombre,
                                FactorConversion = u.EquivalenciaEnUnidades
                            })
                })
                .ToListAsync();

            ViewBag.Productos = productosProveedor;
            ViewBag.ProveedorSeleccionado = compra.IdProveedor;

            return View(compra);
        }

        // 🔹 EDITAR COMPRA (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Compra compra, List<DetalleCompra> detalles)
        {
            if (id != compra.IdCompra)
                return NotFound();

            var compraExistente = await _context.Compras
                .Include(c => c.Detalles)
                .FirstOrDefaultAsync(c => c.IdCompra == id);

            if (compraExistente == null)
                return NotFound();

            if (compraExistente.Estado != "Pendiente")
            {
                TempData["Error"] = "Solo se pueden editar compras en estado Pendiente.";
                return RedirectToAction(nameof(Index), new { estado = compraExistente.Estado });
            }

            detalles = detalles?
                .Where(d => d.IdProducto > 0 && d.Cantidad > 0 && d.PrecioUnitario > 0)
                .ToList() ?? new List<DetalleCompra>();

            if (!detalles.Any())
            {
                TempData["Error"] = "Debe agregar al menos un producto.";
                await CargarDatosVista(compra.IdProveedor);
                return View(compra);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.DetallesCompra.RemoveRange(compraExistente.Detalles);
                await _context.SaveChangesAsync();

                await CalcularTotalesAsync(compraExistente, detalles);

                compraExistente.Observaciones = compra.Observaciones;
                compraExistente.MetodoPago = compra.MetodoPago;
                compraExistente.FechaCompra = FechaLocal.Ahora();

                foreach (var det in detalles)
                {
                    det.IdCompra = compraExistente.IdCompra;
                    det.IdDetalle = 0;
                    _context.DetallesCompra.Add(det);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = idUsuario,
                    Accion = "Edición de compra",
                    Descripcion = $"Compra #{compraExistente.IdCompra} modificada.",
                    Modulo = "Compras",
                    Fecha = FechaLocal.Ahora()
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = "Compra actualizada correctamente.";
                return RedirectToAction(nameof(Index), new { estado = "Pendiente" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Error al actualizar la compra: " + ex.Message;
                await CargarDatosVista(compra.IdProveedor);
                return View(compra);
            }
        }

        // 🔹 CONFIRMAR COMPRA
        [HttpPost]
        public async Task<IActionResult> Confirmar(int id)
        {
            var compra = await _context.Compras
                .Include(c => c.Detalles)
                .FirstOrDefaultAsync(c => c.IdCompra == id);

            if (compra == null)
                return NotFound();

            if (compra.Estado != "Pendiente")
            {
                TempData["Error"] = "Solo se pueden confirmar compras pendientes.";
                return RedirectToAction(nameof(Index), new { estado = compra.Estado });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                compra.Estado = "Confirmada";
                compra.FechaCompra = FechaLocal.Ahora();

                await ActualizarInventarioYPreciosAsync(compra);
                await _context.SaveChangesAsync();

                var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = idUsuario,
                    Accion = "Confirmación de compra",
                    Descripcion = $"Compra #{compra.IdCompra} confirmada.",
                    Modulo = "Compras",
                    Fecha = FechaLocal.Ahora()
                });
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                TempData["Success"] = "Compra confirmada correctamente.";
                return RedirectToAction(nameof(Index), new { estado = "Confirmada" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Error al confirmar compra: " + ex.Message;
                return RedirectToAction(nameof(Index), new { estado = "Pendiente" });
            }
        }

        // 🔹 COMPLETAR COMPRA
        [HttpPost]
        public async Task<IActionResult> Completar(int id)
        {
            var compra = await _context.Compras.FindAsync(id);
            if (compra == null)
                return NotFound();

            if (compra.Estado != "Confirmada")
            {
                TempData["Error"] = "Solo se pueden completar compras confirmadas.";
                return RedirectToAction(nameof(Index), new { estado = compra.Estado });
            }

            compra.Estado = "Completada";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Compra completada correctamente.";
            return RedirectToAction(nameof(Index), new { estado = "Completada" });
        }

        // 🔹 CANCELAR COMPRA
        [HttpPost]
        public async Task<IActionResult> Cancelar(int id)
        {
            var compra = await _context.Compras.FindAsync(id);
            if (compra == null)
                return NotFound();

            if (compra.Estado != "Pendiente")
            {
                TempData["Error"] = "Solo se pueden cancelar compras pendientes.";
                return RedirectToAction(nameof(Index), new { estado = compra.Estado });
            }

            _context.Compras.Remove(compra);
            await _context.SaveChangesAsync();

            var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            _context.BitacoraMovimientos.Add(new BitacoraMovimiento
            {
                IdUsuario = idUsuario,
                Accion = "Cancelación de compra",
                Descripcion = $"Compra #{compra.IdCompra} cancelada.",
                Modulo = "Compras",
                Fecha = FechaLocal.Ahora()
            });
            await _context.SaveChangesAsync();

            TempData["Success"] = "Compra cancelada correctamente.";
            return RedirectToAction(nameof(Index), new { estado = "Pendiente" });
        }

        // 🔹 MÉTODOS AUXILIARES
        private async Task CargarDatosVista(int? idProveedor)
        {
            // Solo proveedores activos que tienen al menos un producto asociado
            ViewBag.Proveedores = await _context.Proveedores
                .Where(p => p.Activo)
                .Where(p => _context.ProductosProveedores.Any(pp => pp.IdProveedor == p.IdProveedor))
                .OrderBy(p => p.Nombre)
                .Select(p => new
                {
                    p.IdProveedor,
                    p.Nombre
                })
                .ToListAsync();

            // Unidades disponibles
            ViewBag.Unidades = await _context.UnidadesMedida
                .Where(u => u.Activo)
                .Select(u => new
                {
                    u.IdUnidad,
                    u.Nombre,
                    u.EquivalenciaEnUnidades
                })
                .ToListAsync();

            ViewBag.ProveedorSeleccionado = idProveedor ?? 0;
        }


        // Valida proveedor + detalles + que el proveedor tenga productos asociados
        private bool ValidarCompra(Compra compra, List<DetalleCompra> detalles)
        {
            if (compra == null || compra.IdProveedor <= 0)
                return false;

            if (detalles == null || !detalles.Any())
                return false;

            // Seguridad extra: no permitir compras a proveedores sin productos asociados
            bool proveedorTieneProductos = _context.ProductosProveedores
                .Any(pp => pp.IdProveedor == compra.IdProveedor);

            return proveedorTieneProductos;
        }

        private async Task CalcularTotalesAsync(Compra compra, List<DetalleCompra> detalles)
        {
            var productosUnidades = await _context.ProductosUnidades
                .Include(pu => pu.UnidadMedida)
                .ToListAsync();

            decimal subtotal = 0;

            foreach (var det in detalles)
            {
                var productoUnidad = productosUnidades
                    .FirstOrDefault(pu => pu.IdProducto == det.IdProducto && pu.IdUnidad == det.IdUnidad);

                decimal equivalencia = productoUnidad?.FactorConversion ?? 1;

                if (det.Descuento > 1)
                    det.Descuento /= 100;

                decimal precioConDescuento = det.PrecioUnitario * (1 - det.Descuento);
                decimal precioBase = precioConDescuento / 1.12m;

                det.Subtotal = det.Cantidad * precioBase * equivalencia;

                subtotal += det.Subtotal;
            }

            compra.Subtotal = subtotal;
            compra.IVA = subtotal * 0.12m;
            compra.Total = compra.Subtotal + compra.IVA;
            compra.FechaCompra = FechaLocal.Ahora();
        }

        private async Task ActualizarInventarioYPreciosAsync(Compra compra)
        {
            var productosProveedores = await _context.ProductosProveedores.ToListAsync();
            var productosUnidades = await _context.ProductosUnidades
                .Include(pu => pu.UnidadMedida)
                .ToListAsync();
            var unidadesGlobales = await _context.UnidadesMedida.ToListAsync();
            var precios = await _context.ProductoPrecio.ToListAsync();

            foreach (var det in compra.Detalles)
            {
                var prodUnidad = productosUnidades
                    .FirstOrDefault(pu => pu.IdProducto == det.IdProducto && pu.IdUnidad == det.IdUnidad);

                decimal factor = prodUnidad?.FactorConversion ?? 1;
                if (prodUnidad == null)
                {
                    var unidadGlobal = unidadesGlobales.FirstOrDefault(u => u.IdUnidad == det.IdUnidad);
                    factor = unidadGlobal?.EquivalenciaEnUnidades ?? 1;
                }

                decimal totalUnidades = det.Cantidad * factor;
                int cantidadEquivalente = (int)Math.Round(totalUnidades, MidpointRounding.AwayFromZero);

                var inventario = await _context.Inventarios.FirstOrDefaultAsync(i => i.IdProducto == det.IdProducto);
                if (inventario != null)
                {
                    inventario.StockActual += cantidadEquivalente;
                    inventario.FechaUltimaActualizacion = FechaLocal.Ahora();
                }
                else
                {
                    _context.Inventarios.Add(new Inventario
                    {
                        IdProducto = det.IdProducto,
                        StockActual = cantidadEquivalente,
                        FechaUltimaActualizacion = FechaLocal.Ahora()
                    });
                }

                var prodProv = productosProveedores
                    .FirstOrDefault(pp => pp.IdProducto == det.IdProducto && pp.IdProveedor == compra.IdProveedor);
                if (prodProv != null)
                {
                    prodProv.CostoCompra = det.PrecioUnitario;
                    prodProv.FechaUltimaCompra = FechaLocal.Ahora();
                }

                if (prodUnidad != null)
                    prodUnidad.PrecioCompra = det.PrecioUnitario;

                decimal precioCompraConIVA = det.PrecioUnitario;
                decimal precioBase = precioCompraConIVA / 1.12m;
                decimal ivaCompra = precioBase * 0.12m;

                const decimal margenUnidad = 0.25m;
                const decimal margenPaquete = 0.15m;
                const decimal margenCaja = 0.10m;

                decimal precioVentaUnidad = Math.Round((precioBase * (1 + margenUnidad)) * 1.12m, 2);
                decimal precioVentaPaquete = Math.Round(((precioBase * 6) * (1 + margenPaquete)) * 1.12m, 2);
                decimal precioVentaCaja = Math.Round(((precioBase * 12) * (1 + margenCaja)) * 1.12m, 2);

                var preciosAntiguos = precios
                    .Where(p => p.IdProducto == det.IdProducto && p.Activo)
                    .ToList();

                foreach (var p in preciosAntiguos)
                {
                    p.Activo = false;
                    p.FechaFin = FechaLocal.Ahora();
                }

                _context.ProductoPrecio.Add(new ProductoPrecio
                {
                    IdProducto = det.IdProducto,
                    PrecioCompra = Math.Round(precioCompraConIVA, 2),
                    PrecioBase = Math.Round(precioBase, 2),
                    IVACompra = Math.Round(ivaCompra, 2),
                    MargenGanancia = margenUnidad,

                    PrecioVentaUnidad = precioVentaUnidad,
                    PrecioVentaPaquete = precioVentaPaquete,
                    PrecioVentaCaja = precioVentaCaja,
                    PrecioVentaSinIVA = Math.Round(precioBase * (1 + margenUnidad), 2),
                    PrecioVenta = precioVentaUnidad,
                    FechaInicio = FechaLocal.Ahora(),
                    Activo = true,
                    UsuarioRegistro = User?.Identity?.Name ?? "Sistema",
                    OrigenCambio = "Compra"
                });

                decimal utilidadUnidad = precioVentaUnidad - precioCompraConIVA;
                decimal utilidadPorCompra = utilidadUnidad * cantidadEquivalente;

                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value),
                    Accion = "Actualización de precios",
                    Descripcion = $"Producto {det.IdProducto}: compra a Q{precioCompraConIVA:N2}, venta unidad Q{precioVentaUnidad:N2}, utilidad estimada total Q{utilidadPorCompra:N2}",
                    Modulo = "Compras",
                    Fecha = FechaLocal.Ahora()
                });
            }

            await _context.SaveChangesAsync();
        }
    }
}
