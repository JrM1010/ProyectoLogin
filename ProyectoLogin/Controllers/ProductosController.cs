using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosProducts;


namespace ProyectoLogin.Controllers
{
    public class ProductosController : Controller
    {
        private readonly DbPruebaContext _context;

        public ProductosController(DbPruebaContext context)
        {
            _context = context;
        }

        // LISTADO - muestra datos generales, stock y precio de venta (último activo)
        public async Task<IActionResult> Index(string q)
        {
            var productos = _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Marca)
                .Include(p => p.Inventario)
                .AsQueryable();

            if (!string.IsNullOrEmpty(q))
            {
                productos = productos.Where(p => p.Nombre.Contains(q) || p.CodigoBarras.Contains(q));
                ViewData["q"] = q;
            }

            var lista = await productos.OrderBy(p => p.Nombre).ToListAsync();

            // Obtener precios activos por producto (último precio activo)
            var precios = await _context.ProductoPrecio
                .Where(pp => pp.Activo)
                .GroupBy(pp => pp.IdProducto)
                .Select(g => g.OrderByDescending(x => x.FechaInicio).FirstOrDefault())
                .ToListAsync();

            ViewBag.Precios = precios;

            return View(lista);
        }

        // GET: Create
        public IActionResult Create()
        {
            ViewBag.Categorias = _context.Categorias.Where(c => c.Activo).ToList();
            ViewBag.Marcas = _context.Marcas.Where(m => m.Activo).ToList();
            // Opcional: unidades de medida para precios/presentaciones
            ViewBag.Unidades = _context.Unidades.ToList();
            return View();
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductoCore producto, int stockMinimo = 0, decimal precioVenta = 0, int unidadParaPrecio = 0)
        {
            if (!ModelState.IsValid)
            {
                ViewBag.Categorias = _context.Categorias.Where(c => c.Activo).ToList();
                ViewBag.Marcas = _context.Marcas.Where(m => m.Activo).ToList();
                ViewBag.Unidades = _context.Unidades.ToList();
                return View(producto);
            }

            // Crear producto
            _context.Productos.Add(producto);
            await _context.SaveChangesAsync();

            // Crear inventario asociado (si no existe)
            var inv = new Inventario
            {
                IdProducto = producto.IdProducto,
                StockActual = 0,
                StockMinimo = 0
            };
            _context.Inventarios.Add(inv);

            // Crear precio inicial si se proporcionó
            if (precioVenta > 0)
            {
                _context.ProductoPrecio.Add(new ProductoPrecio
                {
                    IdProducto = producto.IdProducto,
                    PrecioCompra = 0,
                    PrecioVenta = precioVenta,
                    FechaInicio = DateTime.Now,
                    Activo = true
                });
            }

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // GET: Edit
        public async Task<IActionResult> Edit(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();
            ViewBag.Categorias = _context.Categorias.Where(c => c.Activo).ToList();
            ViewBag.Marcas = _context.Marcas.Where(m => m.Activo).ToList();
            ViewBag.Inventario = await _context.Inventarios.FirstOrDefaultAsync(i => i.IdProducto == id);
            ViewBag.Precios = await _context.ProductoPrecio.Where(p => p.IdProducto == id).OrderByDescending(p => p.FechaInicio).ToListAsync();
            ViewBag.Unidades = _context.Unidades.ToList();




            return View(producto);
        }

        // POST: Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, ProductoCore model)
        {
            if (id != model.IdProducto) return BadRequest();

            if (!ModelState.IsValid)
            {
                ViewBag.Categorias = _context.Categorias.Where(c => c.Activo).ToList();
                ViewBag.Marcas = _context.Marcas.Where(m => m.Activo).ToList();
                return View(model);
            }

            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();

            // Mantener el estado actual de inventario si hay cambio en nombre/categoría/descripcion
            producto.Nombre = model.Nombre;
            producto.Descripcion = model.Descripcion;
            producto.CodigoBarras = model.CodigoBarras;
            producto.IdCategoria = model.IdCategoria;
            producto.IdMarca = model.IdMarca;
            producto.Activo = model.Activo;

            _context.Update(producto);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        // DETALLES
        public async Task<IActionResult> Details(int id)
        {
            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Marca)
                .Include(p => p.Inventario)
                .FirstOrDefaultAsync(p => p.IdProducto == id);

            if (producto == null) return NotFound();

            var precios = await _context.ProductoPrecio.Where(pp => pp.IdProducto == id).OrderByDescending(p => p.FechaInicio).ToListAsync();
            ViewBag.Precios = precios;

            var movimientos = await _context.MovInventarios
                .Where(m => m.IdProducto == id)
                .OrderByDescending(m => m.Fecha)
                .Take(50)
                .ToListAsync();

            ViewBag.Movimientos = movimientos;

            return View(producto);
        }

        // ACTIVAR/DESACTIVAR (toggle)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActivo(int id)
        {
            var producto = await _context.Productos.FindAsync(id);
            if (producto == null) return NotFound();
            producto.Activo = !producto.Activo;
            _context.Update(producto);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        // AGREGAR PRECIO (POST desde Edit o vista parcial)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddPrecio(int idProducto, decimal precioCompra, decimal precioVenta, int? unidadId = null)
        {
            var precio = new ProductoPrecio
            {
                IdProducto = idProducto,
                PrecioCompra = precioCompra,
                PrecioVenta = precioVenta,
                FechaInicio = DateTime.Now,
                Activo = true
            };

            // Desactivar precios anteriores si quieres mantener uno activo por producto:
            var activos = await _context.ProductoPrecio.Where(p => p.IdProducto == idProducto && p.Activo).ToListAsync();
            foreach (var a in activos) { a.Activo = false; _context.Update(a); }

            _context.ProductoPrecio.Add(precio);
            await _context.SaveChangesAsync();

            return RedirectToAction("Edit", new { id = idProducto });
        }

        // Ajuste de stock manual (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjusteStock(int idProducto, decimal cantidad, string tipo, string? referencia = null, string? observacion = null)
        {
            var inv = await _context.Inventarios.FirstOrDefaultAsync(i => i.IdProducto == idProducto);
            if (inv == null)
            {
                inv = new Inventario { IdProducto = idProducto, StockActual = 0, StockMinimo = 0 };
                _context.Inventarios.Add(inv);
            }

            if (tipo == "entrada") inv.StockActual += (int)cantidad;
            else inv.StockActual -= (int)cantidad;

            _context.MovInventarios.Add(new MovInventario
            {
                IdProducto = idProducto,
                Cantidad = cantidad,
                Fecha = DateTime.Now,
                TipoMovimiento = tipo == "entrada" ? "Ajuste entrada" : "Ajuste salida",
                Referencia = referencia,
                Observacion = observacion
            });

            await _context.SaveChangesAsync();
            return RedirectToAction("Details", new { id = idProducto });
        }
    }
}
