using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Recursos;
using System;


namespace ProyectoLogin.Controllers
{
    public class ClientesController : Controller
    {
        private readonly Models.DbPruebaContext _context;
        public ClientesController(Models.DbPruebaContext context)
        {
            _context = context;
        }


        // GET: Clientes
        public async Task<IActionResult> Index(string q)
        {
            var query = _context.Clientes
                .WhereActivo() // ✅ ahora solo clientes activos
                .AsQueryable();

            if (!string.IsNullOrEmpty(q))
                query = query.Where(c =>
                    c.Nombres.Contains(q) ||
                    c.Apellidos.Contains(q) ||
                    c.Correo.Contains(q) ||
                    c.Telefono.Contains(q) ||
                    c.Nit.Contains(q));

            ViewData["q"] = q ?? "";

            var clientes = await query.OrderBy(c => c.Nombres).ToListAsync();

            return View(clientes);
        }

        // GET: Clientes/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null) return NotFound();

            var cliente = await _context.Clientes.AsNoTracking()
                .FirstOrDefaultAsync(m => m.IdCliente == id);

            if (cliente == null) return NotFound();
            return View(cliente);
        }


        // GET: Clientes/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: Clientes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nit,Nombres,Apellidos,Correo,Telefono,Direccion")] Cliente cliente)
        {
            if (ModelState.IsValid)
            {
                cliente.FechaCreacion = DateTime.UtcNow;
                cliente.Activo = true;
                _context.Add(cliente);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(cliente);
        }


        // GET: Clientes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound();
            return View(cliente);
        }

        // POST: Clientes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdCliente,Nit,Nombres,Apellidos,Correo,Telefono,Direccion,Activo")] Cliente cliente)
        {
            if (id != cliente.IdCliente) return NotFound();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cliente);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ClienteExists(cliente.IdCliente)) return NotFound();
                    else throw;
                }
                return RedirectToAction(nameof(Index));
            }
            return View(cliente);
        }


        // GET: Clientes/Delete/5 (confirmación)
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var cliente = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.IdCliente == id);

            if (cliente == null) return NotFound();

            return View(cliente);
        }

        // POST: Clientes/Delete/5 (soft-delete)
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente != null)
            {
                cliente.Activo = false; // soft delete
                _context.Update(cliente);
                await _context.SaveChangesAsync();
            }
            return RedirectToAction(nameof(Index));
        }

        private bool ClienteExists(int id)
        {
            return _context.Clientes.Any(e => e.IdCliente == id);
        }

        // POST: Clientes/ToggleActivo/5 (activar/desactivar)
        [HttpPost]
        public async Task<IActionResult> ToggleActivo(int id)
        {
            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null)
                return NotFound();

            cliente.Activo = !cliente.Activo;
            _context.Update(cliente);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }
    }
}
