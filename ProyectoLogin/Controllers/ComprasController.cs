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
            // Cargar proveedores
            ViewBag.Proveedores = await _context.Proveedores
                .Where(p => p.Activo)
                .ToListAsync();

            // Cargar unidades base (por si acaso)
            ViewBag.Unidades = await _context.UnidadesMedida
                .Where(u => u.Activo)
                .ToListAsync();

            if (idProveedor == null)
            {
                ViewBag.Productos = new List<object>();
                return View("~/Views/Compras/Create.cshtml", new Compra());
            }

            // Productos asociados al proveedor con sus unidades
            var productosProveedor = await _context.ProductosProveedores
                .Include(pp => pp.Producto)
                    .ThenInclude(p => p.ProductosUnidades)
                        .ThenInclude(pu => pu.UnidadMedida)
                .Where(pp => pp.IdProveedor == idProveedor)
                .Select(pp => new
                {
                    pp.Producto.IdProducto,
                    pp.Producto.Nombre,
                    Unidades = pp.Producto.ProductosUnidades.Select(pu => new
                    {
                        pu.IdUnidad,
                        Nombre = pu.UnidadMedida.Nombre,
                        pu.FactorConversion
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
                return RedirectToAction(nameof(Create));
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

                // Calcular subtotal con equivalencia (ej. 2 cajas * 24 unidades * Q500)
                det.Subtotal = det.Cantidad * det.PrecioUnitario * equivalencia;
            }

            // 🔹 Calcular totales generales
            compra.Subtotal = detalles.Sum(d => d.Subtotal);
            compra.IVA = compra.Subtotal * 0.12m;
            compra.Total = compra.Subtotal + compra.IVA;
            compra.FechaCompra = DateTime.Now;
            compra.Estado = "Completada";

            // 🔹 Guardar encabezado y detalles
            _context.Add(compra);
            await _context.SaveChangesAsync();

            foreach (var det in detalles)
            {
                det.IdCompra = compra.IdCompra;
                _context.Add(det);
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = "Compra registrada correctamente.";
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
    }
}
