using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Recursos;

namespace ProyectoLogin.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class ProveedoresController : Controller
    {
        private readonly DbPruebaContext _context;
        private const int PAGE_SIZE = 5;

        public ProveedoresController(DbPruebaContext context)
        {
            _context = context;
        }

        // LISTAR con opción de ordenar y paginación
        public async Task<IActionResult> Index(bool ordenar = false, int pageActivos = 1, int pageInactivos = 1)
        {
            var proveedoresQuery = _context.Proveedores.AsQueryable();

            if (ordenar)
                proveedoresQuery = proveedoresQuery.OrderBy(p => p.Nombre);

            // Separar proveedores activos e inactivos
            var activosQuery = proveedoresQuery.Where(p => p.Activo);
            var inactivosQuery = proveedoresQuery.Where(p => !p.Activo);

            // Calcular paginación para activos
            var totalActivos = await activosQuery.CountAsync();
            var totalPagesActivos = (int)Math.Ceiling(totalActivos / (double)PAGE_SIZE);
            pageActivos = Math.Max(1, Math.Min(pageActivos, totalPagesActivos));

            var activos = await activosQuery
                .Skip((pageActivos - 1) * PAGE_SIZE)
                .Take(PAGE_SIZE)
                .ToListAsync();

            // Calcular paginación para inactivos
            var totalInactivos = await inactivosQuery.CountAsync();
            var totalPagesInactivos = (int)Math.Ceiling(totalInactivos / (double)PAGE_SIZE);
            pageInactivos = Math.Max(1, Math.Min(pageInactivos, totalPagesInactivos));

            var inactivos = await inactivosQuery
                .Skip((pageInactivos - 1) * PAGE_SIZE)
                .Take(PAGE_SIZE)
                .ToListAsync();

            // Pasar datos a la vista
            ViewBag.Activos = activos;
            ViewBag.Inactivos = inactivos;
            ViewBag.TotalActivos = totalActivos;
            ViewBag.TotalInactivos = totalInactivos;
            ViewBag.PageSize = PAGE_SIZE;
            ViewBag.PageActivos = pageActivos;
            ViewBag.PageInactivos = pageInactivos;
            ViewBag.TotalPagesActivos = totalPagesActivos;
            ViewBag.TotalPagesInactivos = totalPagesInactivos;
            ViewBag.Ordenar = ordenar;

            return View(await proveedoresQuery.ToListAsync());
        }

        // CREAR GET
        public IActionResult Create()
        {
            return View();
        }

        // CREAR POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(Proveedor proveedor)
        {
            if (ModelState.IsValid)
            {
                proveedor.Activo = true;
                _context.Proveedores.Add(proveedor);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(proveedor);
        }

        // EDITAR GET
        public async Task<IActionResult> Edit(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor == null) return NotFound();
            return View(proveedor);
        }

        // EDITAR POST
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, Proveedor proveedor)
        {
            if (id != proveedor.IdProveedor) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    var proveedorExistente = await _context.Proveedores.FindAsync(id);
                    if (proveedorExistente == null) return NotFound();

                    // Mantener el estado de "Activo"
                    proveedor.Activo = proveedorExistente.Activo;

                    // Actualizar los demás campos
                    _context.Entry(proveedorExistente).CurrentValues.SetValues(proveedor);

                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    return NotFound();
                }
            }
            return View(proveedor);
        }

        // ELIMINAR (Soft Delete)
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor != null)
            {
                proveedor.Activo = false;
                _context.Update(proveedor);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        // ACTIVAR Proveedor
        [HttpPost]
        public async Task<IActionResult> Activar(int id)
        {
            var proveedor = await _context.Proveedores.FindAsync(id);
            if (proveedor != null)
            {
                proveedor.Activo = true;
                _context.Update(proveedor);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }
    }
}