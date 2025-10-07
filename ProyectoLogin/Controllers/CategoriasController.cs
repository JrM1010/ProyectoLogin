using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;

namespace ProyectoLogin.Controllers
{
    [Authorize(Roles = "Administrador,Gerente")]
    public class CategoriasController : Controller
    {
        private readonly DbPruebaContext _context;

        public CategoriasController(DbPruebaContext context)
        {
            _context = context;
        }

        // GET: Categorias
        public async Task<IActionResult> Index()
        {
            return View(await _context.Categorias.ToListAsync());
        }

        // GET: Categorias/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Categorias/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Categoria categoria)
        {
            if (ModelState.IsValid)
            {
                categoria.Activo = true;
                _context.Add(categoria);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(categoria);
        }

        // GET: Categorias/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria == null)
            {
                return NotFound();
            }
            return View(categoria);
        }

        // POST: Categorias/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Categoria categoria)
        {
            if (id != categoria.IdCategoria)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(categoria);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!CategoriaExists(categoria.IdCategoria))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            return View(categoria);
        }

        // POST: Categorias/Delete/5
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var categoria = await _context.Categorias.FindAsync(id);
            if (categoria != null)
            {
                // Verificar si hay productos usando esta categoría
                var productosConCategoria = await _context.Productos
                    .AnyAsync(p => p.IdCategoria == id && p.Activo);

                if (productosConCategoria)
                {
                    return Json(new { success = false, message = "No se puede eliminar la categoría porque tiene productos asociados." });
                }

                categoria.Activo = false;
                _context.Update(categoria);
                await _context.SaveChangesAsync();
            }

            return Json(new { success = true });
        }

        private bool CategoriaExists(int id)
        {
            return _context.Categorias.Any(e => e.IdCategoria == id);
        }
    }
}
