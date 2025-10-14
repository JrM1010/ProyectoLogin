using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosCompras;
using ProyectoLogin.Models.ModelosProducts;
using ProyectoLogin.Models.UnidadesDeMedida;

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

        // =======================
        // LISTAR COMPRAS
        // =======================
        public async Task<IActionResult> Index()
        {
            var compras = await _context.Compras
                .Include(c => c.Proveedor)
                .OrderByDescending(c => c.FechaCompra)
                .ToListAsync();

            return View("~/Views/Compras/Index.cshtml", compras);
        }

        // =======================
        // GET: CREAR COMPRA
        // =======================
        public async Task<IActionResult> Create(int? idProveedor)
        {
            await CargarDatosVista(idProveedor);

            if (idProveedor == null)
                return View(new Compra());

            // 🔹 Cargar productos del proveedor (con unidades o fallback)
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

            // 🔹 Generar código aleatorio de compra
            var random = new Random();
            ViewBag.NumeroDocumento = random.Next(100000000, 999999999).ToString();

            return View(new Compra { IdProveedor = idProveedor.Value });
        }

        // =======================
        // POST: CREAR COMPRA
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Compra compra, List<DetalleCompra> detalles)
        {
            // 🧹 Filtrar filas vacías
            detalles = detalles
                .Where(d => d.IdProducto > 0 && d.Cantidad > 0 && d.PrecioUnitario > 0)
                .ToList();

            if (!ValidarCompra(compra, detalles))
            {
                TempData["Error"] = "Debe seleccionar un proveedor válido y agregar productos a la compra.";
                await CargarDatosVista(compra.IdProveedor);
                return View(compra);
            }

            // ❌ Eliminar posibles detalles asociados antes de agregar la compra al contexto
            compra.Detalles = null;

            // 🔹 Calcular totales
            await CalcularTotalesAsync(compra, detalles);

            

            // 🔹 Guardar solo el encabezado
            _context.Compras.Add(compra);
            await _context.SaveChangesAsync();

            // 🔹 Guardar detalles manualmente
            await GuardarDetallesYActualizarPreciosAsync(compra, detalles);

            TempData["Success"] = "Compra registrada correctamente.";
            return RedirectToAction(nameof(Index));
        }


        // =======================
        // DETALLES DE COMPRA
        // =======================
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


        // ============================================================
        // 🔹 MÉTODOS AUXILIARES PRIVADOS
        // ============================================================
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
            // 🔹 Cargamos productos con sus unidades y nombres de unidad
            var productosUnidades = await _context.ProductosUnidades
                .Include(pu => pu.UnidadMedida)
                .ToListAsync();

            foreach (var det in detalles)
            {
                var productoUnidad = productosUnidades
                    .FirstOrDefault(pu => pu.IdProducto == det.IdProducto && pu.IdUnidad == det.IdUnidad);

                decimal equivalencia = productoUnidad?.FactorConversion ?? 1;
                decimal descuento = 0;

                // 🔹 Si la unidad es "caja", aplicamos 10% de descuento
                if (productoUnidad?.UnidadMedida?.Nombre?.ToLower() == "caja")
                {
                    descuento = 0.10m;
                }

                det.Descuento = descuento; // ✅ guardamos el descuento aplicado

                // 🔹 Calculamos el subtotal con el descuento incluido
                decimal precioAjustado = det.PrecioUnitario * equivalencia * (1 - descuento);
                det.Subtotal = det.Cantidad * precioAjustado;
            }

            // 🔹 Totales generales
            compra.Subtotal = detalles.Sum(d => d.Subtotal);
            compra.IVA = compra.Subtotal * 0.12m;
            compra.Total = compra.Subtotal + compra.IVA;
            compra.FechaCompra = DateTime.Now;
            compra.Estado = "Completada";
        }



        private async Task GuardarDetallesYActualizarPreciosAsync(Compra compra, List<DetalleCompra> detalles)
        {
            const decimal margenGanancia = 0.25m;

            var productosProveedores = await _context.ProductosProveedores.ToListAsync();
            var productosUnidades = await _context.ProductosUnidades
                .Include(pu => pu.UnidadMedida)
                .ToListAsync();
            var unidadesGlobales = await _context.UnidadesMedida.ToListAsync(); // 🔹 fallback global
            var precios = await _context.ProductoPrecio.ToListAsync();

            foreach (var det in detalles)
            {
                det.IdCompra = compra.IdCompra;
                _context.DetallesCompra.Add(det);

                // 🔹 Buscar primero en ProductosUnidades
                var prodUnidad = productosUnidades
                    .FirstOrDefault(pu => pu.IdProducto == det.IdProducto && pu.IdUnidad == det.IdUnidad);

                decimal factor = prodUnidad?.FactorConversion ?? 1;

                // 🔹 Si no existe, intentar buscar directamente en UnidadesMedida
                if (prodUnidad == null)
                {
                    var unidadGlobal = unidadesGlobales.FirstOrDefault(u => u.IdUnidad == det.IdUnidad);
                    if (unidadGlobal != null)
                        factor = unidadGlobal.EquivalenciaEnUnidades != 0
                            ? unidadGlobal.EquivalenciaEnUnidades
                            : 1;
                }

                // 🔹 Calcular unidades equivalentes
                int cantidadEquivalente = (int)(det.Cantidad * factor);

                // 🔹 Obtener inventario actualizado directamente de la BD
                var inventario = await _context.Inventarios
                    .FirstOrDefaultAsync(i => i.IdProducto == det.IdProducto);

                if (inventario != null)
                {
                    inventario.StockActual += cantidadEquivalente;
                    inventario.FechaUltimaActualizacion = DateTime.Now;
                    _context.Inventarios.Update(inventario);
                }
                else
                {
                    var nuevoInventario = new Inventario
                    {
                        IdProducto = det.IdProducto,
                        StockActual = cantidadEquivalente,
                        StockMinimo = 0,
                        FechaUltimaActualizacion = DateTime.Now
                    };
                    _context.Inventarios.Add(nuevoInventario);
                }

                // 🔹 Actualizar costo proveedor
                var prodProv = productosProveedores
                    .FirstOrDefault(pp => pp.IdProducto == det.IdProducto && pp.IdProveedor == compra.IdProveedor);
                if (prodProv != null)
                {
                    prodProv.CostoCompra = det.PrecioUnitario;
                    prodProv.FechaUltimaCompra = DateTime.Now;
                }

                // 🔹 Actualizar precio compra por unidad
                if (prodUnidad != null)
                    prodUnidad.PrecioCompra = det.PrecioUnitario;

                // 🔹 Desactivar precios antiguos
                var preciosAntiguos = precios
                    .Where(p => p.IdProducto == det.IdProducto && p.Activo)
                    .ToList();

                foreach (var p in preciosAntiguos)
                {
                    p.Activo = false;
                    p.FechaFin = DateTime.Now;
                }

                // 🔹 Crear nuevo precio
                var nuevoPrecio = new ProductoPrecio
                {
                    IdProducto = det.IdProducto,
                    PrecioCompra = det.PrecioUnitario,
                    PrecioVenta = det.PrecioUnitario * (1 + margenGanancia),
                    FechaInicio = DateTime.Now,
                    Activo = true
                };

                _context.ProductoPrecio.Add(nuevoPrecio);
            }

            await _context.SaveChangesAsync();
        }




    }
}
