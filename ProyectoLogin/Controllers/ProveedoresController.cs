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

            public ProveedoresController(DbPruebaContext context)
            {
                _context = context;
            }

        // LISTAR con opción de ordenar
        public async Task<IActionResult> Index(bool ordenar = false)
        {
            var proveedores = _context.Proveedores
                .WhereActivo() // ✅ solo activos
                .AsQueryable();

            if (ordenar)
                proveedores = proveedores.OrderBy(p => p.Nombre);

            return View(await proveedores.ToListAsync());
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

