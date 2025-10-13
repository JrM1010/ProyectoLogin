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
            var compras = await _context.Set<Compra>()
                .Include(c => c.Proveedor)
                .OrderByDescending(c => c.FechaCompra)
                .ToListAsync();

            return View("~/Views/Compras/Index.cshtml", compras);
        }

        // =======================
        // CREAR COMPRA (GET)
        // =======================
        public async Task<IActionResult> Create(int? idProveedor)
        {
            // 🔹 1. Cargar proveedores activos
            ViewBag.Proveedores = await _context.Proveedores
                .Where(p => p.Activo)
                .ToListAsync();

            // 🔹 2. Cargar TODAS las unidades de medida activas
            var unidades = await _context.UnidadesMedida
                .Where(u => u.Activo)
                .Select(u => new
                {
                    u.IdUnidad,
                    u.Nombre,
                    u.EquivalenciaEnUnidades
                })
                .ToListAsync();
            ViewBag.Unidades = unidades;

            // 🔹 Si no hay proveedor seleccionado, aún no mostramos productos
            if (idProveedor == null)
            {
                ViewBag.Productos = new List<object>();
                return View("~/Views/Compras/Create.cshtml", new Compra());
            }

            // 🔹 3. Cargar productos del proveedor con sus posibles unidades (si tiene)
            var productosProveedor = await _context.ProductosProveedores
                .Include(pp => pp.Producto)
                    .ThenInclude(p => p.ProductosUnidades)
                        .ThenInclude(pu => pu.UnidadMedida)
                .Where(pp => pp.IdProveedor == idProveedor)
                .Select(pp => new
                {
                    pp.Producto.IdProducto,
                    pp.Producto.Nombre,
                    // Si el producto no tiene unidades configuradas, cargamos todas las unidades del catálogo
                    Unidades = pp.Producto.ProductosUnidades.Any()
                        ? pp.Producto.ProductosUnidades.Select(pu => new
                        {
                            pu.IdUnidad,
                            Nombre = pu.UnidadMedida.Nombre,
                            pu.FactorConversion
                        })
                        : unidades.Select(u => new
                        {
                            IdUnidad = u.IdUnidad,
                            Nombre = u.Nombre,
                            FactorConversion = u.EquivalenciaEnUnidades
                        })
                })
                .ToListAsync();

            ViewBag.Productos = productosProveedor;
            ViewBag.ProveedorSeleccionado = idProveedor;

            return View("~/Views/Compras/Create.cshtml", new Compra { IdProveedor = idProveedor.Value });
        }




        // =======================
        // CREAR COMPRA (POST)
        // =======================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Compra compra, List<DetalleCompra> detalles)
        {
            if (!ModelState.IsValid || detalles == null || detalles.Count == 0)
            {
                TempData["Error"] = "Debe completar todos los campos y agregar productos a la compra.";
                await CargarDatosVista(compra.IdProveedor);
                return View("~/Views/Compras/Create.cshtml", compra);
            }

            // Si la vista envió Subtotal (por seguridad)
            if (compra.Subtotal == 0 && Request.Form["Subtotal"].Count > 0)
            {
                decimal.TryParse(Request.Form["Subtotal"], out var subtotalForm);
                compra.Subtotal = subtotalForm;
            }

            // 🔹 Recalcular subtotales de manera segura usando equivalencias de la BD
            foreach (var det in detalles)
            {
                // Buscar equivalencia entre unidad seleccionada y producto
                var equivalencia = await _context.ProductosUnidades
                    .Where(pu => pu.IdProducto == det.IdProducto && pu.IdUnidad == det.IdUnidad)
                    .Select(pu => pu.FactorConversion)
                    .FirstOrDefaultAsync();

                if (equivalencia <= 0)
                    equivalencia = 1; // valor por defecto si no hay relación

                // Aplicar descuento por mayoreo si factor > 1
                var descuentoMayoreo = equivalencia > 1 ? 0.10m : 0m;

                // Calcular subtotal con equivalencia y descuento
                var precioAjustado = det.PrecioUnitario * equivalencia * (1 - descuentoMayoreo);

                det.Subtotal = det.Cantidad * precioAjustado;
            }

            // 🔹 Calcular totales generales
            compra.Subtotal = detalles.Sum(d => d.Subtotal);
            compra.IVA = compra.Subtotal * 0.12m;
            compra.Total = compra.Subtotal + compra.IVA;
            compra.FechaCompra = DateTime.Now;
            compra.Estado = "Completada";

            // Guardar encabezado
            _context.Add(compra);
            await _context.SaveChangesAsync();

            // 🔹 Procesar detalles y actualizar precios
            const decimal margenGanancia = 0.25m; // 25% de ganancia

            foreach (var det in detalles)
            {
                det.IdCompra = compra.IdCompra;
                _context.Add(det);

                // ===============================
                // 1️⃣ Actualizar costo en ProductoProveedor
                // ===============================
                var prodProv = await _context.ProductosProveedores
                    .FirstOrDefaultAsync(pp => pp.IdProducto == det.IdProducto && pp.IdProveedor == compra.IdProveedor);

                if (prodProv != null)
                {
                    prodProv.CostoCompra = det.PrecioUnitario;
                    prodProv.FechaUltimaCompra = DateTime.Now;
                    _context.Update(prodProv);
                }

                // ===============================
                // 2️⃣ Actualizar precio de compra en ProductosUnidades
                // ===============================
                var prodUnidad = await _context.ProductosUnidades
                    .FirstOrDefaultAsync(pu => pu.IdProducto == det.IdProducto && pu.IdUnidad == det.IdUnidad);

                if (prodUnidad != null)
                {
                    prodUnidad.PrecioCompra = det.PrecioUnitario;
                    _context.Update(prodUnidad);
                }

                // ===============================
                // 3️⃣ Actualizar precio de venta automático
                // ===============================
                var preciosAntiguos = await _context.ProductoPrecio
                    .Where(p => p.IdProducto == det.IdProducto && p.Activo)
                    .ToListAsync();

                foreach (var precio in preciosAntiguos)
                {
                    precio.Activo = false;
                    precio.FechaFin = DateTime.Now;
                    _context.Update(precio);
                }

                var nuevoPrecioVenta = det.PrecioUnitario * (1 + margenGanancia);

                var nuevoPrecio = new ProductoPrecio
                {
                    IdProducto = det.IdProducto,
                    PrecioCompra = det.PrecioUnitario,
                    PrecioVenta = nuevoPrecioVenta,
                    FechaInicio = DateTime.Now,
                    Activo = true
                };
                _context.ProductoPrecio.Add(nuevoPrecio);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Compra registrada y precios actualizados correctamente.";
            return RedirectToAction(nameof(Index));
        }



        // =======================
        // DETALLES DE COMPRA
        // =======================
        public async Task<IActionResult> Details(int id)
        {
            var compra = await _context.Set<Compra>()
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(c => c.IdCompra == id);

            if (compra == null)
                return NotFound();

            return View("~/Views/Compras/Details.cshtml", compra);
        }


        private async Task CargarDatosVista(int? idProveedor)
        {
            ViewBag.Proveedores = await _context.Proveedores.Where(p => p.Activo).ToListAsync();
            var unidades = await _context.UnidadesMedida
                .Where(u => u.Activo)
                .Select(u => new { u.IdUnidad, u.Nombre, u.EquivalenciaEnUnidades })
                .ToListAsync();
            ViewBag.Unidades = unidades;

            if (idProveedor != null)
            {
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
                            : unidades.Select(u => new
                            {
                                IdUnidad = u.IdUnidad,
                                Nombre = u.Nombre,
                                FactorConversion = u.EquivalenciaEnUnidades
                            })
                    })
                    .ToListAsync();

                ViewBag.Productos = productosProveedor;
                ViewBag.ProveedorSeleccionado = idProveedor;
            }
            else
            {
                ViewBag.Productos = new List<object>();
            }
        }


    }
}
