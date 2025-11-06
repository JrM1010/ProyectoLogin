using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Recursos;
using System;

namespace ProyectoLogin.Controllers
{
    [Authorize(Roles = "Administrador, Vendedor")]
    public class ClientesController : Controller
    {
        private readonly Models.DbPruebaContext _context;
        public ClientesController(Models.DbPruebaContext context)
        {
            _context = context;
        }

        // Método para normalizar el NIT (quitar guiones)
        private string NormalizarNit(string nit)
        {
            if (string.IsNullOrEmpty(nit))
                return nit;

            // Quitar todos los guiones y espacios
            return nit.Replace("-", "").Replace(" ", "").ToUpper();
        }

        // Método para formatear el NIT para la vista (agregar guion)
        private string FormatearNitParaVista(string nit)
        {
            if (string.IsNullOrEmpty(nit) || nit.Contains("-"))
                return nit;

            // Si el NIT tiene 9 o más caracteres, agregar guion antes del último carácter
            if (nit.Length >= 9)
            {
                return nit.Insert(nit.Length - 1, "-");
            }

            return nit;
        }

        
        // GET: Clientes
        public async Task<IActionResult> Index(string q, int pageActivos = 1, int pageInactivos = 1, int pageSize = 5)
        {
            // Consulta base para clientes activos
            var queryActivos = _context.Clientes
                .Where(c => c.Activo)
                .AsQueryable();

            // Consulta base para clientes inactivos
            var queryInactivos = _context.Clientes
                .Where(c => !c.Activo)
                .AsQueryable();

            // Aplicar búsqueda si existe
            if (!string.IsNullOrEmpty(q))
            {
                var qNormalizado = NormalizarNit(q);

                queryActivos = queryActivos.Where(c =>
                    c.Nombres.Contains(q) ||
                    c.Apellidos.Contains(q) ||
                    c.Correo.Contains(q) ||
                    c.Nit.Contains(q) ||
                    c.Nit.Contains(qNormalizado));

                queryInactivos = queryInactivos.Where(c =>
                    c.Nombres.Contains(q) ||
                    c.Apellidos.Contains(q) ||
                    c.Correo.Contains(q) ||
                    c.Nit.Contains(q) ||
                    c.Nit.Contains(qNormalizado));
            }

            // Ordenar
            queryActivos = queryActivos.OrderBy(c => c.Nombres);
            queryInactivos = queryInactivos.OrderBy(c => c.Nombres);

            // Obtener totales
            var totalActivos = await queryActivos.CountAsync();
            var totalInactivos = await queryInactivos.CountAsync();

            // Calcular páginas totales
            var totalPagesActivos = (int)Math.Ceiling(totalActivos / (double)pageSize);
            var totalPagesInactivos = (int)Math.Ceiling(totalInactivos / (double)pageSize);

            // Asegurar que las páginas estén en rango válido
            pageActivos = Math.Max(1, Math.Min(pageActivos, totalPagesActivos));
            pageInactivos = Math.Max(1, Math.Min(pageInactivos, totalPagesInactivos));

            // Aplicar paginación
            var clientesActivos = await queryActivos
                .Skip((pageActivos - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var clientesInactivos = await queryInactivos
                .Skip((pageInactivos - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            // Pasar datos a la vista
            ViewData["q"] = q ?? "";
            ViewBag.Activos = clientesActivos;
            ViewBag.Inactivos = clientesInactivos;
            ViewBag.TotalActivos = totalActivos;
            ViewBag.TotalInactivos = totalInactivos;
            ViewBag.PageSize = pageSize;
            ViewBag.PageActivos = pageActivos;
            ViewBag.PageInactivos = pageInactivos;
            ViewBag.TotalPagesActivos = totalPagesActivos;
            ViewBag.TotalPagesInactivos = totalPagesInactivos;

            return View();
        }



        
        // GET: Clientes/Create
        public IActionResult Create()
        {
            return View();
        }

        
        // POST: Clientes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("Nit,Nombres,Apellidos,Correo,Direccion")] Cliente cliente)
        {
            // Normalizar el NIT antes de las validaciones
            if (!string.IsNullOrEmpty(cliente.Nit))
            {
                cliente.Nit = NormalizarNit(cliente.Nit);
            }

            // Validación personalizada para NIT único
            if (!string.IsNullOrEmpty(cliente.Nit))
            {
                var nitExistente = await _context.Clientes
                    .AnyAsync(c => c.Nit == cliente.Nit && c.Activo);

                if (nitExistente)
                {
                    ModelState.AddModelError("Nit", "Ya existe un cliente activo con este NIT");
                }
            }

            // Validación personalizada para correo único
            if (!string.IsNullOrEmpty(cliente.Correo))
            {
                var correoExistente = await _context.Clientes
                    .AnyAsync(c => c.Correo == cliente.Correo && c.Activo);

                if (correoExistente)
                {
                    ModelState.AddModelError("Correo", "Ya existe un cliente activo con este correo electrónico");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    cliente.FechaCreacion = DateTime.UtcNow;
                    cliente.Activo = true;
                    _context.Add(cliente);
                    await _context.SaveChangesAsync();

                    TempData["Mensaje"] = "Cliente creado exitosamente";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    ModelState.AddModelError("", "Error al guardar el cliente. Por favor, verifique los datos.");
                }
            }
            else
            {
                // Si hay errores, restaurar el NIT con formato para mostrar en la vista
                if (cliente.Nit != null && !cliente.Nit.Contains("-") && cliente.Nit.Length >= 9)
                {
                    cliente.Nit = FormatearNitParaVista(cliente.Nit);
                }
            }

            return View(cliente);
        }


        [Authorize(Roles = "Administrador")]
        // GET: Clientes/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null) return NotFound();

            var cliente = await _context.Clientes.FindAsync(id);
            if (cliente == null) return NotFound();

            // Formatear el NIT para mostrarlo con guion en la vista de edición
            if (!string.IsNullOrEmpty(cliente.Nit) && !cliente.Nit.Contains("-") && cliente.Nit.Length >= 9)
            {
                cliente.Nit = FormatearNitParaVista(cliente.Nit);
            }

            return View(cliente);
        }


        [Authorize(Roles = "Administrador")]
        // POST: Clientes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("IdCliente,Nit,Nombres,Apellidos,Correo,Direccion,Activo")] Cliente cliente)
        {
            if (id != cliente.IdCliente) return NotFound();

            // Normalizar el NIT antes de las validaciones
            if (!string.IsNullOrEmpty(cliente.Nit))
            {
                cliente.Nit = NormalizarNit(cliente.Nit);
            }

            // Validación personalizada para NIT único (excluyendo el actual)
            if (!string.IsNullOrEmpty(cliente.Nit))
            {
                var nitExistente = await _context.Clientes
                    .AnyAsync(c => c.Nit == cliente.Nit && c.IdCliente != id && c.Activo);

                if (nitExistente)
                {
                    ModelState.AddModelError("Nit", "Ya existe otro cliente activo con este NIT");
                }
            }

            // Validación personalizada para correo único (excluyendo el actual)
            if (!string.IsNullOrEmpty(cliente.Correo))
            {
                var correoExistente = await _context.Clientes
                    .AnyAsync(c => c.Correo == cliente.Correo && c.IdCliente != id && c.Activo);

                if (correoExistente)
                {
                    ModelState.AddModelError("Correo", "Ya existe otro cliente activo con este correo electrónico");
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(cliente);
                    await _context.SaveChangesAsync();

                    TempData["Mensaje"] = "Cliente actualizado exitosamente";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ClienteExists(cliente.IdCliente)) return NotFound();
                    else throw;
                }
                catch (DbUpdateException ex)
                {
                    ModelState.AddModelError("", "Error al actualizar el cliente. Por favor, verifique los datos.");
                }
            }
            else
            {
                // Si hay errores, restaurar el NIT con formato para mostrar en la vista
                if (cliente.Nit != null && !cliente.Nit.Contains("-") && cliente.Nit.Length >= 9)
                {
                    cliente.Nit = FormatearNitParaVista(cliente.Nit);
                }
            }
            return View(cliente);
        }


        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null) return NotFound();

            var cliente = await _context.Clientes
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.IdCliente == id);

            if (cliente == null) return NotFound();

            return View(cliente);
        }

        [Authorize(Roles = "Administrador")]
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

                TempData["Mensaje"] = "Cliente desactivado exitosamente";
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

            TempData["Mensaje"] = cliente.Activo ? "Cliente activado exitosamente" : "Cliente desactivado exitosamente";
            return RedirectToAction(nameof(Index));
        }
    }
}