using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosProducts;

namespace ProyectoLogin.Controllers
{
    
        [Authorize(Roles = "Administrador")]
        public class ProductosController : Controller
        {
            private readonly DbPruebaContext _context;

            public ProductosController(DbPruebaContext context)
            {
                _context = context;
            }

            // LISTAR
            public async Task<IActionResult> Index(string q)
            {
            var productos = _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Marca)
                .Include(p => p.Inventario)
                .AsQueryable();

            if (!string.IsNullOrEmpty(q))
            {
                productos = productos.Where(p =>
                    p.Nombre.Contains(q) ||
                    p.CodigoBarras.Contains(q));
            }

            // ✅ Guarda el texto de búsqueda en ViewData
            ViewData["q"] = q;

            return View(await productos.ToListAsync());
        }

            // CREAR GET
            public IActionResult Create()
            {
                ViewBag.Categorias = _context.Categorias.Where(c => c.Activo).ToList();
                ViewBag.Marcas = _context.Marcas.Where(m => m.Activo).ToList();
                return View();
            }

            // CREAR POST
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Create(ProductoCore producto)
            {
                if (ModelState.IsValid)
                {
                    _context.Productos.Add(producto);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                ViewBag.Categorias = _context.Categorias.Where(c => c.Activo).ToList();
                ViewBag.Marcas = _context.Marcas.Where(m => m.Activo).ToList();
                return View(producto);
            }

            // EDITAR GET
            public async Task<IActionResult> Edit(int id)
            {
                var producto = await _context.Productos.FindAsync(id);
                if (producto == null) return NotFound();

                ViewBag.Categorias = _context.Categorias.Where(c => c.Activo).ToList();
                ViewBag.Marcas = _context.Marcas.Where(m => m.Activo).ToList();
                return View(producto);
            }

            // EDITAR POST
            [HttpPost]
            [ValidateAntiForgeryToken]
            public async Task<IActionResult> Edit(int id, ProductoCore producto)
            {
                if (id != producto.IdProducto) return NotFound();

                if (ModelState.IsValid)
                {
                    try
                    {
                        _context.Update(producto);
                        await _context.SaveChangesAsync();
                        return RedirectToAction(nameof(Index));
                    }
                    catch (DbUpdateConcurrencyException)
                    {
                        return NotFound();
                    }
                }
                ViewBag.Categorias = _context.Categorias.Where(c => c.Activo).ToList();
                ViewBag.Marcas = _context.Marcas.Where(m => m.Activo).ToList();
                return View(producto);
            }

            // ELIMINAR (soft delete)
            [HttpPost]
            public async Task<IActionResult> Delete(int id)
            {
                var producto = await _context.Productos.FindAsync(id);
                if (producto != null)
                {
                    producto.Activo = false;
                    _context.Update(producto);
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Index));
            }

            // ACTIVAR
            [HttpPost]
            public async Task<IActionResult> Activar(int id)
            {
                var producto = await _context.Productos.FindAsync(id);
                if (producto != null)
                {
                    producto.Activo = true;
                    _context.Update(producto);
                    await _context.SaveChangesAsync();
                }
                return RedirectToAction(nameof(Index));
            }

            // DETALLES
            public async Task<IActionResult> Details(int id)
            {
                var producto = await _context.Productos
                    .Include(p => p.Categoria)
                    .Include(p => p.Marca)
                    .FirstOrDefaultAsync(p => p.IdProducto == id);

                if (producto == null) return NotFound();
                return View(producto);
            }
        }
}

