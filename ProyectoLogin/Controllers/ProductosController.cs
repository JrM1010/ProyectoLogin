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
        public async Task<IActionResult> Index(string q)
        {
            var productos = _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Marca)
                .Include(p => p.Inventario)
                .WhereActivo() // ✅ ahora solo productos activos
                .AsQueryable();

            if (!string.IsNullOrEmpty(q))
            {
                productos = productos.Where(p => p.Nombre.Contains(q) || p.CodigoBarras.Contains(q));
                ViewData["q"] = q;
            }

            var lista = await productos.OrderBy(p => p.Nombre).ToListAsync();

            var precios = await _context.ProductoPrecio
                .Where(pp => pp.Activo)
                .GroupBy(pp => pp.IdProducto)
                .Select(g => g.OrderByDescending(x => x.FechaInicio).FirstOrDefault())
                .ToListAsync();

            ViewBag.Precios = precios;

            return View(lista);
        }

        // GET: Create
        public async Task<IActionResult> Create()
        {
            // Cargar categorías
            var categorias = await _context.Categorias
                .OrderBy(c => c.Nombre)
                .ToListAsync();

            // Cargar marcas
            var marcas = await _context.Marcas
                .Where(m => m.Activo)
                .OrderBy(m => m.Nombre)
                .ToListAsync();

            // Cargar proveedores
            var proveedores = await _context.Proveedores
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            // Asignaciones a ViewBag para la vista
            ViewBag.CategoriasSelect = new SelectList(categorias, "IdCategoria", "Nombre");
            ViewBag.CategoriasLista = categorias;

            ViewBag.MarcasSelect = new SelectList(marcas, "IdMarca", "Nombre");
            ViewBag.Proveedores = proveedores;
            ViewBag.MarcasLista = marcas;
            // Código sugerido
            ViewBag.CodigoGenerado = $"PROD-{DateTime.UtcNow:yyyyMMdd-HHmmss}-{new Random().Next(0, 10000):D4}";

            return View();
        }

        // POST: Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ProductoCore producto, int stockMinimo = 0, int idProveedor = 0)
        {

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

            // ✅ Categorías (dos versiones)
            ViewBag.CategoriasSelect = new SelectList(categorias, "IdCategoria", "Nombre");
            ViewBag.CategoriasLista = categorias;

            // ✅ Marcas (convertida correctamente)
            ViewBag.MarcasSelect = new SelectList(marcas, "IdMarca", "Nombre");

            // ✅ Proveedores (no usa asp-items, así que queda lista normal)
            ViewBag.Proveedores = proveedores;

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
                    _context.ProductosProveedores.Add(rel);
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
                codigo = $"PRO-{FechaLocal.Ahora:yyyyMMdd-HHmmss}-{sufijo}";
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
            foreach (var a in activos) { a.Activo = false; _context.Update(a); }

            _context.ProductoPrecio.Add(precio);
            await _context.SaveChangesAsync();

            return RedirectToAction("Edit", new { id = idProducto });
        }

        // Ajuste de stock manual (POST)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjusteStock(int idProducto, int cantidad, string tipo, string? referencia = null, string? observacion = null)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var inventario = await _context.Inventarios.FirstOrDefaultAsync(i => i.IdProducto == idProducto);

                if (inventario == null)
                {
                    inventario = new Inventario
                    {
                        IdProducto = idProducto,
                        StockActual = 0,
                        StockMinimo = 0,
                        FechaUltimaActualizacion = FechaLocal.Ahora()
                    };
                    _context.Inventarios.Add(inventario);
                }

                // ✅ Validación de stock y tipo de movimiento
                if (tipo == "entrada")
                {
                    inventario.StockActual += cantidad;
                }
                else if (tipo == "salida")
                {
                    if (inventario.StockActual < cantidad)
                    {
                        TempData["Error"] = "No se puede realizar la salida. Stock insuficiente.";
                        return RedirectToAction("Details", new { id = idProducto });
                    }
                    inventario.StockActual -= cantidad;
                }
                else
                {
                    TempData["Error"] = "Tipo de movimiento no válido.";
                    return RedirectToAction("Details", new { id = idProducto });
                }

                inventario.FechaUltimaActualizacion = FechaLocal.Ahora();

                // Registrar movimiento
                var movimiento = new MovInventario
                {
                    IdProducto = idProducto,
                    Cantidad = cantidad,
                    Fecha = FechaLocal.Ahora(),
                    TipoMovimiento = tipo == "entrada" ? "Ajuste entrada" : "Ajuste salida",
                    Referencia = referencia,
                    Observacion = observacion
                };

                _context.MovInventarios.Add(movimiento);

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                TempData["Mensaje"] = $"Ajuste de stock ({movimiento.TipoMovimiento}) realizado correctamente.";
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                TempData["Error"] = $"Error al ajustar el stock: {ex.Message}";
            }

            return RedirectToAction("Details", new { id = idProducto });
        }




        // =======================
        // CATEGORÍAS
        // =======================
        [HttpPost]
        public async Task<IActionResult> CrearCategoria(string nombre, string descripcion)
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
        public async Task<IActionResult> EditarCategoria(int id, string nombre, string descripcion)
        {
            TempData["AbrirModal"] = "Categoria";

            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null)
            {
                TempData["MensajeCategoria"] = "Categoría no encontrada.";
                TempData["TipoCategoria"] = "danger";
                return RedirectToAction("Create", "Productos");
            }

            categoria.Nombre = nombre;
            categoria.Descripcion = descripcion;
            await _context.SaveChangesAsync();

            TempData["MensajeCategoria"] = "Categoría actualizada correctamente.";
            TempData["TipoCategoria"] = "info";
            return RedirectToAction("Create", "Productos");
        }

        [HttpPost]
        public async Task<IActionResult> EliminarCategoria(int id)
        {
            TempData["AbrirModal"] = "Categoria";

            var tieneProductos = await _context.Productos.AnyAsync(p => p.IdCategoria == id);
            if (tieneProductos)
            {
                TempData["MensajeCategoria"] = "No se puede eliminar: hay productos asociados.";
                TempData["TipoCategoria"] = "warning";
                return RedirectToAction("Create", "Productos");
            }

            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria != null)
            {
                _context.Categorias.Remove(categoria);
                await _context.SaveChangesAsync();

                TempData["MensajeCategoria"] = "Categoría eliminada correctamente.";
                TempData["TipoCategoria"] = "danger";
            }

            return RedirectToAction("Create", "Productos");
        }


        // =======================
        // MARCAS
        // =======================
        [HttpPost]
        public async Task<IActionResult> CrearMarca(string nombre)
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

        [HttpPost]
        public async Task<IActionResult> EditarMarca(int id, string nombre)
        {
            TempData["AbrirModal"] = "Marca";

            var marca = await _context.Marcas.FindAsync(id);
            if (marca == null)
            {
                TempData["MensajeMarca"] = "Marca no encontrada.";
                TempData["TipoMarca"] = "danger";
                return RedirectToAction("Create", "Productos");
            }

            marca.Nombre = nombre;
            await _context.SaveChangesAsync();

            TempData["MensajeMarca"] = "Marca actualizada correctamente.";
            TempData["TipoMarca"] = "info";
            return RedirectToAction("Create", "Productos");
        }

        [HttpPost]
        public async Task<IActionResult> EliminarMarca(int id)
        {
            TempData["AbrirModal"] = "Marca";

            var tieneProductos = await _context.Productos.AnyAsync(p => p.IdMarca == id);
            if (tieneProductos)
            {
                TempData["MensajeMarca"] = "No se puede eliminar: hay productos asociados.";
                TempData["TipoMarca"] = "warning";
                return RedirectToAction("Create", "Productos");
            }

            var marca = await _context.Marcas.FindAsync(id);
            if (marca != null)
            {
                _context.Marcas.Remove(marca);
                await _context.SaveChangesAsync();

                TempData["MensajeMarca"] = "Marca eliminada correctamente.";
                TempData["TipoMarca"] = "danger";
            }

            return RedirectToAction("Create", "Productos");
        }

    }
}
