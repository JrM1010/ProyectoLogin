using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosProducts;
using ProyectoLogin.Recursos;


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
        public async Task<IActionResult> Index(string q, int pageActivos = 1, int pageInactivos = 1, int pageSize = 5)
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

            // Separar activos e inactivos
            var activos = lista.Where(p => p.Activo).ToList();
            var inactivos = lista.Where(p => !p.Activo).ToList();

            // Aplicar paginación independiente
            var activosPaginados = activos.Skip((pageActivos - 1) * pageSize).Take(pageSize).ToList();
            var inactivosPaginados = inactivos.Skip((pageInactivos - 1) * pageSize).Take(pageSize).ToList();

            // Calcular total de páginas
            ViewBag.TotalActivos = activos.Count;
            ViewBag.TotalInactivos = inactivos.Count;
            ViewBag.PageSize = pageSize;
            ViewBag.PageActivos = pageActivos;
            ViewBag.PageInactivos = pageInactivos;
            ViewBag.TotalPagesActivos = (int)Math.Ceiling(activos.Count / (double)pageSize);
            ViewBag.TotalPagesInactivos = (int)Math.Ceiling(inactivos.Count / (double)pageSize);

            var precios = await _context.ProductoPrecio
                .Where(pp => pp.Activo)
                .GroupBy(pp => pp.IdProducto)
                .Select(g => g.OrderByDescending(x => x.FechaInicio).FirstOrDefault())
                .ToListAsync();

            ViewBag.Precios = precios;

            // Pasar las listas paginadas a la vista
            ViewBag.Activos = activosPaginados;
            ViewBag.Inactivos = inactivosPaginados;

            return View(lista);
        }

        // GET: Create
        public async Task<IActionResult> Create()
        {
            // Categorias
            var categorias = await _context.Categorias
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            // Marcas
            var marcas = await _context.Marcas
                .Where(m => m.Activo)
                .OrderBy(m => m.Nombre)
                .ToListAsync();

            // Proveedores
            var proveedores = await _context.Proveedores
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombre)
                .ToListAsync();


            ViewBag.CategoriasSelect = new SelectList(categorias, "IdCategoria", "Nombre");
            ViewBag.CategoriasLista = categorias;

            ViewBag.MarcasSelect = new SelectList(marcas, "IdMarca", "Nombre");
            ViewBag.Proveedores = proveedores;
            ViewBag.MarcasLista = marcas;

            // Código sugerido
            ViewBag.CodigoGenerado = $"PROD-{new Random().Next(0, 10000):D4}";

            return View();
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductoCore producto, int stockMinimo = 0, int idProveedor = 0)
        {
            // Cargar datos para la vista (en caso de error)
            var categorias = await _context.Categorias
                .Where(c => c.Activo)
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            var marcas = await _context.Marcas
                .Where(m => m.Activo)
                .OrderBy(m => m.Nombre)
                .ToListAsync();

            var proveedores = await _context.Proveedores
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            ViewBag.CategoriasSelect = new SelectList(categorias, "IdCategoria", "Nombre");
            ViewBag.CategoriasLista = categorias;
            ViewBag.MarcasSelect = new SelectList(marcas, "IdMarca", "Nombre");
            ViewBag.Proveedores = proveedores;
            ViewBag.MarcasLista = marcas;
            ViewBag.CodigoGenerado = $"PROD-{new Random().Next(0, 10000):D4}";

            // Validaciones adicionales
            if (idProveedor <= 0)
            {
                ModelState.AddModelError("idProveedor", "Debe seleccionar un proveedor.");
            }

            if (string.IsNullOrWhiteSpace(producto.Descripcion))
            {
                ModelState.AddModelError("Descripcion", "Las especificaciones del producto son obligatorias.");
            }

            if (!ModelState.IsValid)
            {
                return View(producto);
            }

            try
            {
                // Si no trae código, generamos uno único
                if (string.IsNullOrWhiteSpace(producto.CodigoBarras))
                {
                    producto.CodigoBarras = await GenerarCodigoProductoAsync();
                }

                producto.Activo = true;
                producto.FechaCreacion = DateTime.UtcNow;
                _context.Productos.Add(producto);
                await _context.SaveChangesAsync();

                var invExistente = await _context.Inventarios.FirstOrDefaultAsync(i => i.IdProducto == producto.IdProducto);
                if (invExistente == null)
                {
                    var inv = new Inventario
                    {
                        IdProducto = producto.IdProducto,
                        StockActual = 0,
                        StockMinimo = stockMinimo,
                        FechaUltimaActualizacion = DateTime.UtcNow
                    };
                    _context.Inventarios.Add(inv);
                }
                else
                {
                    invExistente.StockMinimo = stockMinimo;
                    _context.Inventarios.Update(invExistente);
                }

                // Relacionar proveedor
                if (idProveedor > 0)
                {
                    var rel = new ProductoProveedor
                    {
                        IdProducto = producto.IdProducto,
                        IdProveedor = idProveedor,
                        CostoCompra = 0, // inicial, se actualizará en compras
                        FechaUltimaCompra = null
                    };
                    _context.ProductosProveedores.Add(rel);
                }

                await _context.SaveChangesAsync();

                // Registrar en bitácora
                var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = idUsuario,
                    Accion = "Creación de producto",
                    Descripcion = $"Producto '{producto.Nombre}' creado exitosamente.",
                    Modulo = "Productos",
                    Fecha = DateTime.UtcNow
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = "Producto creado exitosamente.";
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
                codigo = $"PRO-{sufijo}";
                intentos++;

                // Evita bucle infinito: si muchos choques (improbable), genera GUID como fallback
                if (intentos > 10)
                {
                    codigo = "PRO-" + Guid.NewGuid().ToString("N").Substring(0, 12).ToUpper();
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
                FechaInicio = FechaLocal.Ahora(),
                Activo = true
            };

            // Desactivar precios anteriores si quieres mantener uno activo por producto:
            var activos = await _context.ProductoPrecio.Where(p => p.IdProducto == idProducto && p.Activo).ToListAsync();
            foreach (var a in activos)
            {
                a.Activo = false;
                a.FechaFin = FechaLocal.Ahora();
                _context.Update(a);
            }

            _context.ProductoPrecio.Add(precio);
            await _context.SaveChangesAsync();

            return RedirectToAction("Edit", new { id = idProducto });
        }

        // ========== MÉTODOS AJAX PARA CATEGORÍAS ==========

        [HttpGet]
        public async Task<IActionResult> ObtenerCategorias()
        {
            var categorias = await _context.Categorias
                .Where(c => c.Activo)
                .OrderBy(c => c.Nombre)
                .Select(c => new {
                    idCategoria = c.IdCategoria,
                    nombre = c.Nombre,
                    descripcion = c.Descripcion
                })
                .ToListAsync();

            return Json(categorias);
        }

        [HttpPost]
        public async Task<IActionResult> CrearCategoria(string nombre, string descripcion)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombre))
                {
                    return Json(new { success = false, message = "El nombre de la categoría es obligatorio." });
                }

                var existe = await _context.Categorias.AnyAsync(c => c.Nombre == nombre);
                if (existe)
                {
                    return Json(new { success = false, message = "Ya existe una categoría con ese nombre." });
                }

                var categoria = new Categoria
                {
                    Nombre = nombre,
                    Descripcion = descripcion,
                    Activo = true
                };

                _context.Categorias.Add(categoria);
                await _context.SaveChangesAsync();

                var categorias = await _context.Categorias
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Nombre)
                    .Select(c => new { idCategoria = c.IdCategoria, nombre = c.Nombre })
                    .ToListAsync();

                return Json(new { success = true, message = "Categoría creada correctamente.", categorias });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al crear la categoría: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditarCategoria(int id, string nombre, string descripcion)
        {
            try
            {
                var categoria = await _context.Categorias.FindAsync(id);
                if (categoria == null)
                {
                    return Json(new { success = false, message = "Categoría no encontrada." });
                }

                // Verificar si el nombre ya existe en otra categoría
                var nombreExiste = await _context.Categorias
                    .AnyAsync(c => c.Nombre == nombre && c.IdCategoria != id);

                if (nombreExiste)
                {
                    return Json(new { success = false, message = "Ya existe una categoría con ese nombre." });
                }

                categoria.Nombre = nombre;
                categoria.Descripcion = descripcion;
                await _context.SaveChangesAsync();

                var categorias = await _context.Categorias
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Nombre)
                    .Select(c => new { idCategoria = c.IdCategoria, nombre = c.Nombre })
                    .ToListAsync();

                return Json(new { success = true, message = "Categoría actualizada correctamente.", categorias });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al actualizar la categoría: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EliminarCategoria(int id)
        {
            try
            {
                var tieneProductos = await _context.Productos.AnyAsync(p => p.IdCategoria == id);
                if (tieneProductos)
                {
                    return Json(new { success = false, message = "No se puede eliminar: hay productos asociados." });
                }

                var categoria = await _context.Categorias.FindAsync(id);
                if (categoria == null)
                {
                    return Json(new { success = false, message = "Categoría no encontrada." });
                }

                _context.Categorias.Remove(categoria);
                await _context.SaveChangesAsync();

                var categorias = await _context.Categorias
                    .Where(c => c.Activo)
                    .OrderBy(c => c.Nombre)
                    .Select(c => new { idCategoria = c.IdCategoria, nombre = c.Nombre })
                    .ToListAsync();

                return Json(new { success = true, message = "Categoría eliminada correctamente.", categorias });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al eliminar la categoría: " + ex.Message });
            }
        }

        // ========== MÉTODOS AJAX PARA MARCAS ==========

        [HttpGet]
        public async Task<IActionResult> ObtenerMarcas()
        {
            var marcas = await _context.Marcas
                .Where(m => m.Activo)
                .OrderBy(m => m.Nombre)
                .Select(m => new {
                    idMarca = m.IdMarca,
                    nombre = m.Nombre,
                    activo = m.Activo
                })
                .ToListAsync();

            return Json(marcas);
        }

        [HttpPost]
        public async Task<IActionResult> CrearMarca(string nombre)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(nombre))
                {
                    return Json(new { success = false, message = "El nombre de la marca es obligatorio." });
                }

                var existe = await _context.Marcas.AnyAsync(m => m.Nombre == nombre);
                if (existe)
                {
                    return Json(new { success = false, message = "Ya existe una marca con ese nombre." });
                }

                var marca = new Marca { Nombre = nombre, Activo = true };
                _context.Marcas.Add(marca);
                await _context.SaveChangesAsync();

                var marcas = await _context.Marcas
                    .Where(m => m.Activo)
                    .OrderBy(m => m.Nombre)
                    .Select(m => new { idMarca = m.IdMarca, nombre = m.Nombre })
                    .ToListAsync();

                return Json(new { success = true, message = "Marca creada correctamente.", marcas });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al crear la marca: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EditarMarca(int id, string nombre)
        {
            try
            {
                var marca = await _context.Marcas.FindAsync(id);
                if (marca == null)
                {
                    return Json(new { success = false, message = "Marca no encontrada." });
                }

                // Verificar si el nombre ya existe en otra marca
                var nombreExiste = await _context.Marcas
                    .AnyAsync(m => m.Nombre == nombre && m.IdMarca != id);

                if (nombreExiste)
                {
                    return Json(new { success = false, message = "Ya existe una marca con ese nombre." });
                }

                marca.Nombre = nombre;
                await _context.SaveChangesAsync();

                var marcas = await _context.Marcas
                    .Where(m => m.Activo)
                    .OrderBy(m => m.Nombre)
                    .Select(m => new { idMarca = m.IdMarca, nombre = m.Nombre })
                    .ToListAsync();

                return Json(new { success = true, message = "Marca actualizada correctamente.", marcas });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al actualizar la marca: " + ex.Message });
            }
        }

        [HttpPost]
        public async Task<IActionResult> EliminarMarca(int id)
        {
            try
            {
                var tieneProductos = await _context.Productos.AnyAsync(p => p.IdMarca == id);
                if (tieneProductos)
                {
                    return Json(new { success = false, message = "No se puede eliminar: hay productos asociados." });
                }

                var marca = await _context.Marcas.FindAsync(id);
                if (marca == null)
                {
                    return Json(new { success = false, message = "Marca no encontrada." });
                }

                _context.Marcas.Remove(marca);
                await _context.SaveChangesAsync();

                var marcas = await _context.Marcas
                    .Where(m => m.Activo)
                    .OrderBy(m => m.Nombre)
                    .Select(m => new { idMarca = m.IdMarca, nombre = m.Nombre })
                    .ToListAsync();

                return Json(new { success = true, message = "Marca eliminada correctamente.", marcas });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Error al eliminar la marca: " + ex.Message });
            }
        }

        // ========== MÉTODOS LEGACY (mantenidos para compatibilidad) ==========

        [HttpPost]
        public async Task<IActionResult> CrearCategoriaLegacy(string nombre, string descripcion)
        {
            TempData["AbrirModal"] = "Categoria";

            if (string.IsNullOrWhiteSpace(nombre))
            {
                TempData["MensajeCategoria"] = "El nombre de la categoría es obligatorio.";
                TempData["TipoCategoria"] = "warning";
                return RedirectToAction("Create", "Productos");
            }

            var existe = await _context.Categorias.AnyAsync(c => c.Nombre == nombre);
            if (existe)
            {
                TempData["MensajeCategoria"] = "Ya existe una categoría con ese nombre.";
                TempData["TipoCategoria"] = "warning";
                return RedirectToAction("Create", "Productos");
            }

            _context.Categorias.Add(new Categoria
            {
                Nombre = nombre,
                Descripcion = descripcion,
                Activo = true
            });
            await _context.SaveChangesAsync();

            TempData["MensajeCategoria"] = "Categoría creada correctamente.";
            TempData["TipoCategoria"] = "success";
            return RedirectToAction("Create", "Productos");
        }

        [HttpPost]
        public async Task<IActionResult> CrearMarcaLegacy(string nombre)
        {
            TempData["AbrirModal"] = "Marca";

            if (string.IsNullOrWhiteSpace(nombre))
            {
                TempData["MensajeMarca"] = "El nombre de la marca es obligatorio.";
                TempData["TipoMarca"] = "warning";
                return RedirectToAction("Create", "Productos");
            }

            var existe = await _context.Marcas.AnyAsync(m => m.Nombre == nombre);
            if (existe)
            {
                TempData["MensajeMarca"] = "Ya existe una marca con ese nombre.";
                TempData["TipoMarca"] = "warning";
                return RedirectToAction("Create", "Productos");
            }

            _context.Marcas.Add(new Marca { Nombre = nombre, Activo = true });
            await _context.SaveChangesAsync();

            TempData["MensajeMarca"] = "Marca creada correctamente.";
            TempData["TipoMarca"] = "success";
            return RedirectToAction("Create", "Productos");
        }

    }
}