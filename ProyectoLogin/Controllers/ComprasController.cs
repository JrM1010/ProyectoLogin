using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosCompras;
using ProyectoLogin.Models.ModelosProducts;

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

            // Cargar productos (vista simple)
            ViewBag.Productos = await _context.Set<ProductoCore>()
                .Where(p => p.Activo)
                .ToListAsync();
            
            // Cargar unidades (fijas)
            ViewBag.Unidades = new List<UnidadMedida>
            {
              new UnidadMedida { IdUnidad = 1, Nombre = "Unidad" },
              new UnidadMedida { IdUnidad = 2, Nombre = "Caja" }
            };

            // Si no hay proveedor seleccionado aún
            if (idProveedor == null)
            {
                ViewBag.Productos = new List<ProyectoLogin.Models.ModelosProducts.ProductoCore>();
                return View("~/Views/Compras/Create.cshtml", new Compra());
            }

            // Cargar productos asociados al proveedor seleccionado
            var productosProveedor = await _context.ProductosProveedores
                .Include(pp => pp.Producto)
                .Where(pp => pp.IdProveedor == idProveedor)
                .Select(pp => new ProyectoLogin.Models.ModelosProducts.ProductoCore
                {
                    IdProducto = pp.Producto.IdProducto,
                    Nombre = pp.Producto.Nombre,
                    Descripcion = pp.Producto.Descripcion,
                    CodigoBarras = pp.Producto.CodigoBarras,
                    Activo = pp.Producto.Activo
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

            // 🟩 Si la vista envió Subtotal, úsalo como respaldo
            if (compra.Subtotal == 0 && Request.Form["Subtotal"].Count > 0)
            {
                decimal.TryParse(Request.Form["Subtotal"], out var subtotalForm);
                compra.Subtotal = subtotalForm;
            }

            // Recalcular subtotales por seguridad
            foreach (var det in detalles)
            {
                // Descuento del 5% si la unidad es caja
                if (det.IdUnidad == 12)
                    det.PrecioUnitario *= 0.95m;

                det.Subtotal = det.Cantidad * det.PrecioUnitario;
            }

            // Si no venía del form, calcular desde los detalles
            if (compra.Subtotal == 0)
                compra.Subtotal = detalles.Sum(d => d.Subtotal);

            compra.IVA = compra.Subtotal * 0.12m;
            compra.Total = compra.Subtotal + compra.IVA;
            compra.FechaCompra = DateTime.Now;
            compra.Estado = "Completada";

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
