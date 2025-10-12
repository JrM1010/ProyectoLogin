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

            ViewBag.CodigoGenerado = $"PROD-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{new Random().Next(0, 10000):D4}";
            ViewBag.Proveedores = _context.Proveedores.Where(p => p.Activo).ToList();

            return View();
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductoCore producto, int stockMinimo = 0, int idProveedor = 0)
        {
            ViewBag.Categorias = _context.Categorias.Where(c => c.Activo).ToList();
            ViewBag.Marcas = _context.Marcas.Where(m => m.Activo).ToList();
            ViewBag.Unidades = _context.Unidades.ToList();
            ViewBag.Proveedores = _context.Proveedores.Where(p => p.Activo).ToList();

            if (!ModelState.IsValid)
            {
                return View(producto);
            }

            try
            {
                // Si no trae código (o quieres siempre sobrescribir), generamos uno único
                if (string.IsNullOrWhiteSpace(producto.CodigoBarras))
                {
                    producto.CodigoBarras = await GenerarCodigoProductoAsync();
                }

                producto.Activo = true;
                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();

                var invExistente = await _context.Inventarios.FirstOrDefaultAsync(i => i.IdProducto == producto.IdProducto);
                if (invExistente == null)
                {
                    var inv = new Inventario
                    {
                        IdProducto = producto.IdProducto,
                        StockActual = 0,
                        StockMinimo = stockMinimo
                    };
                    _context.Inventarios.Add(inv);


                }
                else
                {
                    
                    invExistente.StockMinimo = stockMinimo;
                    _context.Inventarios.Update(invExistente);
                }

                // Relacionar proveedor (si se eligió uno)
                if (idProveedor > 0)
                {
                    var rel = new ProductoProveedor
                    {
                        IdProducto = producto.IdProducto,
                        IdProveedor = idProveedor,
                        CostoCompra = 0, // inicial, se actualizará en compras
                        FechaUltimaCompra = null
                    };
                    _context.ProductoProveedor.Add(rel);
                }


                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            catch (Exception ex)
            {
                ModelState.AddModelError("", "Error guardando producto: " + ex.Message);
                return View(producto);
            }
        }

        // Genera un código legible y chequea unicidad (async)
        private async Task<string> GenerarCodigoProductoAsync()
        {
            
            string codigo;
            var rnd = new Random();
            int intentos = 0;

            do
            {
                var sufijo = rnd.Next(0, 10000).ToString("D4"); // 0000..9999
                codigo = $"PROD-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{sufijo}";
                intentos++;
                // Evita bucle infinito: si muchos choques (improbable), genera GUID como fallback
                if (intentos > 10)
                {
                    codigo = "PROD-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
                    break;
                }
            }
            while (await _context.Productos.AnyAsync(p => p.CodigoBarras == codigo));

            return codigo;
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
        public async Task<IActionResult> Edit(int id, ProductoCore model, int stockMinimo)
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

            // Actualizar stock mínimo del inventario
            var inventario = await _context.Inventarios.FirstOrDefaultAsync(i => i.IdProducto == id);
            if (inventario != null)
            {
                inventario.StockMinimo = stockMinimo;
                _context.Update(inventario);
            }

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
