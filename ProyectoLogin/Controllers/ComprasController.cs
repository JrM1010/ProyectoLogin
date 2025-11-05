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

            // Configuración de paginación
            int elementosPorPagina = 10;
            int elementosASaltar = (pagina - 1) * elementosPorPagina;

            // Consulta base
            var consulta = _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(c => c.Estado == estado)
                .OrderByDescending(c => c.FechaCompra)
                .AsSplitQuery();

            // Aplicar filtro por proveedor si se especifica
            if (!string.IsNullOrEmpty(proveedor))
            {
                consulta = consulta.Where(c => c.Proveedor.Nombre.Contains(proveedor));
            }

            // Obtener el total de elementos para la paginación
            int totalElementos = await consulta.CountAsync();
            int totalPaginas = (int)Math.Ceiling(totalElementos / (double)elementosPorPagina);

            // Aplicar paginación
            var compras = await consulta
                .Skip(elementosASaltar)
                .Take(elementosPorPagina)
                .ToListAsync();

            // Datos para la vista
            ViewBag.EstadoActual = estado;
            ViewBag.Estados = new List<string> { "Pendiente", "Confirmada", "Completada" };
            ViewBag.ProveedorFiltro = proveedor;
            ViewBag.PaginaActual = pagina;
            ViewBag.TotalPaginas = totalPaginas;
            ViewBag.TotalElementos = totalElementos;
            ViewBag.ElementosPorPagina = elementosPorPagina;

            // Obtener lista de proveedores para el dropdown
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

            if (idProveedor == null)
                return View(new Compra());

            var productosProveedor = await _context.ProductosProveedores
                .Include(pp => pp.Producto)
                    .ThenInclude(p => p.ProductosUnidades)
                        .ThenInclude(pu => pu.UnidadMedida)
                .Where(pp => pp.IdProveedor == idProveedor)
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
            ViewBag.ProveedorSeleccionado = idProveedor;

            var random = new Random();
            ViewBag.NumeroDocumento = random.Next(100000000, 999999999).ToString();

            return View(new Compra
            {
                IdProveedor = idProveedor.Value,
                Estado = "Pendiente"
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
                TempData["Error"] = "Debe seleccionar un proveedor válido y agregar productos.";
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
                _context.Compras.Add(compra);
                await _context.SaveChangesAsync();

                foreach (var det in detalles)
                {
                    det.IdCompra = compra.IdCompra;
                    _context.DetallesCompra.Add(det);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 🔹 Registrar movimiento en bitácora
                var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = idUsuario,
                    Accion = "Registro de compra",
                    Descripcion = $"Compra #{compra.IdCompra} creada.",
                    Modulo = "Compras",
                    Fecha = FechaLocal.Ahora()
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = "Compra creada correctamente.";
                return RedirectToAction(nameof(Index), new { estado = "Pendiente" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Error al registrar la compra: " + ex.Message;
                await CargarDatosVista(compra.IdProveedor);
                return View(compra);
            }
        }

        // 🔹 DETALLES DE COMPRA
        public async Task<IActionResult> Details(int id)
        {
            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.UnidadMedida) // ahora válido
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
                // 🧹 Eliminar detalles antiguos
                _context.DetallesCompra.RemoveRange(compraExistente.Detalles);
                await _context.SaveChangesAsync();

                // 🔢 Calcular nuevos totales
                await CalcularTotalesAsync(compraExistente, detalles);

                // ✏️ Actualizar datos de la compra
                compraExistente.Observaciones = compra.Observaciones;
                compraExistente.MetodoPago = compra.MetodoPago;
                compraExistente.FechaCompra = FechaLocal.Ahora();

                // 🧩 Agregar nuevos detalles
                foreach (var det in detalles)
                {
                    det.IdCompra = compraExistente.IdCompra;
                    det.IdDetalle = 0;
                    _context.DetallesCompra.Add(det);
                }

                // 💾 Guardar y confirmar
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();


                // 🔹 Registrar movimiento en bitácora
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


                // 🔹 Registrar movimiento en bitácora
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

            // 🔹 Registrar movimiento en bitácora
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
            ViewBag.Proveedores = await _context.Proveedores
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            ViewBag.Unidades = await _context.UnidadesMedida
                .Where(u => u.Activo)
                .Select(u => new { u.IdUnidad, u.Nombre, u.EquivalenciaEnUnidades })
                .ToListAsync();

            ViewBag.ProveedorSeleccionado = idProveedor ?? 0;
        }

        private static bool ValidarCompra(Compra compra, List<DetalleCompra> detalles)
        {
            return compra.IdProveedor > 0 && detalles.Any();
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

                // ✅ Si el descuento viene en porcentaje (0–100), convertir a decimal (0–1)
                if (det.Descuento > 1)
                    det.Descuento /= 100;

                // ✅ Calcular precio total con descuento (ya incluye IVA)
                decimal precioConDescuento = det.PrecioUnitario * (1 - det.Descuento);

                // ✅ Calcular precio base (sin IVA) y el IVA de ese producto
                decimal precioBase = precioConDescuento / 1.12m;
                decimal ivaItem = precioBase * 0.12m;

                // ✅ Subtotal de la fila (sin IVA)
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
            const decimal margenGanancia = 0.25m; // ✅ Ganancia sobre el precio base sin IVA

            var productosProveedores = await _context.ProductosProveedores.ToListAsync();
            var productosUnidades = await _context.ProductosUnidades
                .Include(pu => pu.UnidadMedida)
                .ToListAsync();
            var unidadesGlobales = await _context.UnidadesMedida.ToListAsync();
            var precios = await _context.ProductoPrecio.ToListAsync();

            foreach (var det in compra.Detalles)
            {
                // ============================
                // 🔹 ACTUALIZACIÓN DE INVENTARIO
                // ============================
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

                // ============================
                // 🔹 ACTUALIZAR RELACIÓN PRODUCTO-PROVEEDOR
                // ============================
                var prodProv = productosProveedores
                    .FirstOrDefault(pp => pp.IdProducto == det.IdProducto && pp.IdProveedor == compra.IdProveedor);
                if (prodProv != null)
                {
                    prodProv.CostoCompra = det.PrecioUnitario; // incluye IVA
                    prodProv.FechaUltimaCompra = FechaLocal.Ahora();
                }

                if (prodUnidad != null)
                {
                    prodUnidad.PrecioCompra = det.PrecioUnitario;
                }

                // ============================
                // 🔹 CÁLCULO DE PRECIOS (COMPRAS Y VENTAS)
                // ============================
                decimal precioCompraConIVA = det.PrecioUnitario;            // precio que viene del proveedor
                decimal precioBase = precioCompraConIVA / 1.12m;             // costo sin IVA
                decimal ivaCompra = precioBase * 0.12m;                      // IVA de compra
                decimal precioVentaSinIVA = precioBase * (1 + margenGanancia);
                decimal precioVentaConIVA = precioVentaSinIVA * 1.12m;

                // ============================
                // 🔹 DESACTIVAR PRECIOS ANTIGUOS
                // ============================
                var preciosAntiguos = precios
                    .Where(p => p.IdProducto == det.IdProducto && p.Activo)
                    .ToList();

                foreach (var p in preciosAntiguos)
                {
                    p.Activo = false;
                    p.FechaFin = FechaLocal.Ahora();
                }

                // ============================
                // 🔹 CREAR NUEVO REGISTRO DE PRECIO (AUDITABLE)
                // ============================
                _context.ProductoPrecio.Add(new ProductoPrecio
                {
                    IdProducto = det.IdProducto,
                    PrecioCompra = precioCompraConIVA,     // compra con IVA
                    PrecioBase = precioBase,               // costo sin IVA
                    IVACompra = ivaCompra,                 // iva calculado
                    MargenGanancia = margenGanancia,       // margen aplicado
                    PrecioVentaSinIVA = precioVentaSinIVA, // venta sin IVA
                    PrecioVenta = precioVentaConIVA,       // venta con IVA
                    FechaInicio = FechaLocal.Ahora(),
                    Activo = true,
                    UsuarioRegistro = User?.Identity?.Name ?? "Sistema",
                    OrigenCambio = "Compra"
                });
            }
        }

    }
}
