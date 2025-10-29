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

        // LISTAR COMPRAS - Mostrar según estado
        public async Task<IActionResult> Index(string estado = "Pendiente")
        {
            var compras = await _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .Where(c => c.Estado == estado)
                .OrderByDescending(c => c.FechaCompra)
                .ToListAsync();

            ViewBag.EstadoActual = estado;
            ViewBag.Estados = new List<string> { "Pendiente", "Confirmada", "Completada" };

            return View("~/Views/Compras/Index.cshtml", compras);
        }

        // GET: CREAR COMPRA (igual que antes)
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
                Estado = "Pendiente" // Estado inicial
            });
        }

        // POST: CREAR COMPRA (modificado para estado Pendiente)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Compra compra, List<DetalleCompra> detalles)
        {
            // Filtrar filas vacías
            detalles = detalles
                .Where(d => d.IdProducto > 0 && d.Cantidad > 0 && d.PrecioUnitario > 0)
                .ToList();

            if (!ValidarCompra(compra, detalles))
            {
                TempData["Error"] = "Debe seleccionar un proveedor válido y agregar productos a la compra.";
                await CargarDatosVista(compra.IdProveedor);
                return View(compra);
            }

            // Validar cantidades enteras
            foreach (var det in detalles)
            {
                if (det.Cantidad % 1 != 0)
                {
                    TempData["Error"] = $"La cantidad del producto con ID {det.IdProducto} debe ser un número entero.";
                    await CargarDatosVista(compra.IdProveedor);
                    return View(compra);
                }
            }

            // Estado inicial
            compra.Estado = "Pendiente";
            compra.FechaCompra = FechaLocal.Ahora();
            compra.Detalles = null;

            await CalcularTotalesAsync(compra, detalles);

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Guardar encabezado
                _context.Compras.Add(compra);
                await _context.SaveChangesAsync();

                // Guardar detalles (sin actualizar inventario todavía)
                foreach (var det in detalles)
                {
                    det.IdCompra = compra.IdCompra;
                    _context.DetallesCompra.Add(det);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Compra creada correctamente. Estado: Pendiente";
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

        // GET: EDITAR COMPRA (solo para estado Pendiente)
        public async Task<IActionResult> Edit(int id)
        {
            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(c => c.IdCompra == id);

            if (compra == null)
                return NotFound();

            if (compra.Estado != "Pendiente")
            {
                TempData["Error"] = "Solo se pueden editar compras en estado Pendiente";
                return RedirectToAction(nameof(Index), new { estado = compra.Estado });
            }

            await CargarDatosVista(compra.IdProveedor);

            // Cargar productos del proveedor
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

        // POST: EDITAR COMPRA
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
                TempData["Error"] = "Solo se pueden editar compras en estado Pendiente";
                return RedirectToAction(nameof(Index), new { estado = compraExistente.Estado });
            }

            // Filtrar detalles válidos
            detalles = detalles?
                .Where(d => d.IdProducto > 0 && d.Cantidad > 0 && d.PrecioUnitario > 0)
                .ToList() ?? new List<DetalleCompra>();

            if (!detalles.Any())
            {
                TempData["Error"] = "La compra debe tener al menos un producto";
                await CargarDatosVista(compra.IdProveedor);
                return View(compra);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Eliminar detalles antiguos
                _context.DetallesCompra.RemoveRange(compraExistente.Detalles);

                // Recalcular totales
                await CalcularTotalesAsync(compraExistente, detalles);

                // Actualizar datos básicos
                compraExistente.Observaciones = compra.Observaciones;
                compraExistente.MetodoPago = compra.MetodoPago;

                // Agregar nuevos detalles
                foreach (var det in detalles)
                {
                    det.IdCompra = compraExistente.IdCompra;
                    _context.DetallesCompra.Add(det);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Compra actualizada correctamente";
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

        // CONFIRMAR COMPRA (pasa a estado Confirmada y actualiza inventario)
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
                TempData["Error"] = "Solo se pueden confirmar compras en estado Pendiente";
                return RedirectToAction(nameof(Index), new { estado = compra.Estado });
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Actualizar estado
                compra.Estado = "Confirmada";
                compra.FechaCompra = FechaLocal.Ahora();

                // Actualizar inventario y precios (método existente)
                await ActualizarInventarioYPreciosAsync(compra);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Success"] = "Compra confirmada correctamente. Inventario actualizado.";
                return RedirectToAction(nameof(Index), new { estado = "Confirmada" });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = "Error al confirmar la compra: " + ex.Message;
                return RedirectToAction(nameof(Index), new { estado = "Pendiente" });
            }
        }

        // COMPLETAR COMPRA
        [HttpPost]
        public async Task<IActionResult> Completar(int id)
        {
            var compra = await _context.Compras.FindAsync(id);
            if (compra == null)
                return NotFound();

            if (compra.Estado != "Confirmada")
            {
                TempData["Error"] = "Solo se pueden completar compras en estado Confirmada";
                return RedirectToAction(nameof(Index), new { estado = compra.Estado });
            }

            compra.Estado = "Completada";
            await _context.SaveChangesAsync();

            TempData["Success"] = "Compra marcada como completada";
            return RedirectToAction(nameof(Index), new { estado = "Completada" });
        }

        // CANCELAR COMPRA
        [HttpPost]
        public async Task<IActionResult> Cancelar(int id)
        {
            var compra = await _context.Compras.FindAsync(id);
            if (compra == null)
                return NotFound();

            // Solo se pueden cancelar compras pendientes
            if (compra.Estado != "Pendiente")
            {
                TempData["Error"] = "Solo se pueden cancelar compras en estado Pendiente";
                return RedirectToAction(nameof(Index), new { estado = compra.Estado });
            }

            _context.Compras.Remove(compra);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Compra cancelada y eliminada";
            return RedirectToAction(nameof(Index), new { estado = "Pendiente" });
        }

        // MÉTODO PARA ACTUALIZAR INVENTARIO (separado del guardado inicial)
        private async Task ActualizarInventarioYPreciosAsync(Compra compra)
        {
            const decimal margenGanancia = 0.25m;

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
                    if (unidadGlobal != null)
                        factor = unidadGlobal.EquivalenciaEnUnidades != 0
                            ? unidadGlobal.EquivalenciaEnUnidades
                            : 1;
                }

                decimal totalUnidades = det.Cantidad * factor;
                if (totalUnidades % 1 != 0)
                {
                    throw new InvalidOperationException(
                        $"El total de unidades ({totalUnidades}) para el producto {det.IdProducto} no es un número entero.");
                }

                int cantidadEquivalente = (int)Math.Round(totalUnidades, MidpointRounding.AwayFromZero);

                // Actualizar inventario
                var inventario = await _context.Inventarios
                    .FirstOrDefaultAsync(i => i.IdProducto == det.IdProducto);

                if (inventario != null)
                {
                    inventario.StockActual += cantidadEquivalente;
                    inventario.FechaUltimaActualizacion = FechaLocal.Ahora();
                    _context.Inventarios.Update(inventario);
                }
                else
                {
                    var nuevoInventario = new Inventario
                    {
                        IdProducto = det.IdProducto,
                        StockActual = cantidadEquivalente,
                        StockMinimo = 0,
                        FechaUltimaActualizacion = FechaLocal.Ahora()
                    };
                    _context.Inventarios.Add(nuevoInventario);
                }

                // Actualizar costo proveedor
                var prodProv = productosProveedores
                    .FirstOrDefault(pp => pp.IdProducto == det.IdProducto && pp.IdProveedor == compra.IdProveedor);
                if (prodProv != null)
                {
                    prodProv.CostoCompra = det.PrecioUnitario;
                    prodProv.FechaUltimaCompra = FechaLocal.Ahora();
                    _context.ProductosProveedores.Update(prodProv);
                }

                // Actualizar precio compra por presentación
                if (prodUnidad != null)
                {
                    prodUnidad.PrecioCompra = det.PrecioUnitario;
                    _context.ProductosUnidades.Update(prodUnidad);
                }

                // Desactivar precios antiguos
                var preciosAntiguos = precios
                    .Where(p => p.IdProducto == det.IdProducto && p.Activo)
                    .ToList();

                foreach (var p in preciosAntiguos)
                {
                    p.Activo = false;
                    p.FechaFin = FechaLocal.Ahora();
                    _context.ProductoPrecio.Update(p);
                }

                // Crear nuevo precio
                var nuevoPrecio = new ProductoPrecio
                {
                    IdProducto = det.IdProducto,
                    PrecioCompra = det.PrecioUnitario,
                    PrecioVenta = det.PrecioUnitario * (1 + margenGanancia),
                    FechaInicio = FechaLocal.Ahora(),
                    Activo = true
                };

                _context.ProductoPrecio.Add(nuevoPrecio);
            }
        }

        // DETALLES DE COMPRA (sin cambios)
        public async Task<IActionResult> Details(int id)
        {
            var compra = await _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(c => c.IdCompra == id);

            if (compra == null)
                return NotFound();

            return View("~/Views/Compras/Details.cshtml", compra);
        }

        // 🔹 MÉTODOS AUXILIARES PRIVADOS (sin cambios)
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
            return compra.IdProveedor > 0 && detalles != null && detalles.Any();
        }

        private async Task CalcularTotalesAsync(Compra compra, List<DetalleCompra> detalles)
        {
            var productosUnidades = await _context.ProductosUnidades
                .Include(pu => pu.UnidadMedida)
                .ToListAsync();

            foreach (var det in detalles)
            {
                var productoUnidad = productosUnidades
                    .FirstOrDefault(pu => pu.IdProducto == det.IdProducto && pu.IdUnidad == det.IdUnidad);

                decimal equivalencia = productoUnidad?.FactorConversion ?? 1;
                decimal descuento = 0;

                if (productoUnidad?.UnidadMedida?.Nombre?.ToLower() == "caja")
                {
                    descuento = 0.10m;
                }

                if (productoUnidad?.UnidadMedida?.Nombre?.ToLower() == "paquete")
                {
                    descuento = 0.05m;
                }

                det.Descuento = descuento;

                decimal precioAjustado = det.PrecioUnitario * equivalencia * (1 - descuento);
                det.Subtotal = det.Cantidad * precioAjustado;
            }

            compra.Subtotal = detalles.Sum(d => d.Subtotal);
            compra.IVA = compra.Subtotal * 0.12m;
            compra.Total = compra.Subtotal + compra.IVA;
            compra.FechaCompra = FechaLocal.Ahora();
        }
    }
}