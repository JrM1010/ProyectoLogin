using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Recursos;
using System.ComponentModel.DataAnnotations;

namespace ProyectoLogin.Controllers
{
    [Authorize(Roles = "Administrador")]
    public class ProveedoresController : Controller
    {
        private readonly DbPruebaContext _context;
        private const int PAGE_SIZE = 5;
        private readonly ILogger<ProveedoresController> _logger;

        public ProveedoresController(DbPruebaContext context, ILogger<ProveedoresController> logger)
        {
            _context = context;
            _logger = logger;
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
            // Validaciones manuales adicionales
            await AplicarValidacionesPersonalizadas(proveedor);

            if (ModelState.IsValid)
            {
                try
                {
                    // Validar unicidad del nombre
                    var proveedorExistente = await _context.Proveedores
                        .FirstOrDefaultAsync(p => p.Nombre.ToLower() == proveedor.Nombre.ToLower() && p.Activo);

                    if (proveedorExistente != null)
                    {
                        ModelState.AddModelError("Nombre", "Ya existe un proveedor activo con este nombre.");
                        return View(proveedor);
                    }

                    // Validar unicidad del email si se proporciona
                    if (!string.IsNullOrWhiteSpace(proveedor.Email))
                    {
                        var emailExistente = await _context.Proveedores
                            .FirstOrDefaultAsync(p => p.Email.ToLower() == proveedor.Email.ToLower() && p.Activo);

                        if (emailExistente != null)
                        {
                            ModelState.AddModelError("Email", "Ya existe un proveedor activo con este email.");
                            return View(proveedor);
                        }
                    }

                    proveedor.Activo = true;
                    _context.Proveedores.Add(proveedor);
                    await _context.SaveChangesAsync();

                    // 🔹 Registrar en bitácora
                    var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                    _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                    {
                        IdUsuario = idUsuario,
                        Accion = "Creación de proveedor",
                        Descripcion = $"Proveedor '{proveedor.Nombre}' creado exitosamente.",
                        Modulo = "Proveedores",
                        Fecha = FechaLocal.Ahora()
                    });
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Proveedor creado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex)
                {
                    _logger.LogError(ex, "Error al crear proveedor {Nombre}", proveedor.Nombre);
                    ModelState.AddModelError("", "Error al guardar el proveedor. Verifique los datos e intente nuevamente.");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inesperado al crear proveedor {Nombre}", proveedor.Nombre);
                    ModelState.AddModelError("", "Error inesperado al guardar el proveedor.");
                }
            }

            // Si llegamos aquí, algo salió mal, volver a mostrar el formulario
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

            // Validaciones manuales adicionales
            await AplicarValidacionesPersonalizadas(proveedor, id);

            if (ModelState.IsValid)
            {
                try
                {
                    var proveedorExistente = await _context.Proveedores.FindAsync(id);
                    if (proveedorExistente == null) return NotFound();

                    // Validar unicidad del nombre (excluyendo el actual)
                    var nombreExistente = await _context.Proveedores
                        .FirstOrDefaultAsync(p => p.Nombre.ToLower() == proveedor.Nombre.ToLower()
                                               && p.Activo
                                               && p.IdProveedor != id);

                    if (nombreExistente != null)
                    {
                        ModelState.AddModelError("Nombre", "Ya existe un proveedor activo con este nombre.");
                        return View(proveedor);
                    }

                    // Validar unicidad del email (excluyendo el actual)
                    if (!string.IsNullOrWhiteSpace(proveedor.Email))
                    {
                        var emailExistente = await _context.Proveedores
                            .FirstOrDefaultAsync(p => p.Email.ToLower() == proveedor.Email.ToLower()
                                                   && p.Activo
                                                   && p.IdProveedor != id);

                        if (emailExistente != null)
                        {
                            ModelState.AddModelError("Email", "Ya existe un proveedor activo con este email.");
                            return View(proveedor);
                        }
                    }

                    // Mantener el estado de "Activo"
                    proveedor.Activo = proveedorExistente.Activo;

                    // Actualizar los demás campos
                    _context.Entry(proveedorExistente).CurrentValues.SetValues(proveedor);

                    await _context.SaveChangesAsync();

                    // 🔹 Registrar en bitácora
                    var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                    _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                    {
                        IdUsuario = idUsuario,
                        Accion = "Edición de proveedor",
                        Descripcion = $"Proveedor '{proveedor.Nombre}' actualizado exitosamente.",
                        Modulo = "Proveedores",
                        Fecha = FechaLocal.Ahora()
                    });
                    await _context.SaveChangesAsync();

                    TempData["Success"] = "Proveedor actualizado exitosamente.";
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException ex)
                {
                    _logger.LogError(ex, "Error de concurrencia al editar proveedor {Id}", id);
                    if (!await ProveedorExists(id))
                        return NotFound();
                    else
                        throw;
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error inesperado al editar proveedor {Id}", id);
                    ModelState.AddModelError("", "Error inesperado al actualizar el proveedor.");
                }
            }
            return View(proveedor);
        }

        // ELIMINAR (Soft Delete)
        [HttpPost]
        public async Task<IActionResult> Delete(int id)
        {
            try
            {
                var proveedor = await _context.Proveedores.FindAsync(id);
                if (proveedor == null)
                {
                    TempData["Error"] = "Proveedor no encontrado.";
                    return RedirectToAction(nameof(Index));
                }

                // Verificar si el proveedor tiene productos asociados
                var tieneProductos = await _context.ProductosProveedores
                    .AnyAsync(pp => pp.IdProveedor == id);

                if (tieneProductos)
                {
                    TempData["Error"] = "No se puede desactivar el proveedor porque tiene productos asociados.";
                    return RedirectToAction(nameof(Index));
                }

                proveedor.Activo = false;
                _context.Update(proveedor);
                await _context.SaveChangesAsync();

                // 🔹 Registrar en bitácora
                var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = idUsuario,
                    Accion = "Desactivación de proveedor",
                    Descripcion = $"Proveedor '{proveedor.Nombre}' desactivado.",
                    Modulo = "Proveedores",
                    Fecha = FechaLocal.Ahora()
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = "Proveedor desactivado exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al desactivar proveedor {Id}", id);
                TempData["Error"] = "Error al desactivar el proveedor.";
            }

            return RedirectToAction(nameof(Index));
        }

        // ACTIVAR Proveedor
        [HttpPost]
        public async Task<IActionResult> Activar(int id)
        {
            try
            {
                var proveedor = await _context.Proveedores.FindAsync(id);
                if (proveedor == null)
                {
                    TempData["Error"] = "Proveedor no encontrado.";
                    return RedirectToAction(nameof(Index));
                }

                // Validar que no exista otro proveedor activo con el mismo nombre
                var nombreExistente = await _context.Proveedores
                    .FirstOrDefaultAsync(p => p.Nombre.ToLower() == proveedor.Nombre.ToLower()
                                           && p.Activo
                                           && p.IdProveedor != id);

                if (nombreExistente != null)
                {
                    TempData["Error"] = "Ya existe un proveedor activo con el mismo nombre. No se puede reactivar.";
                    return RedirectToAction(nameof(Index));
                }

                proveedor.Activo = true;
                _context.Update(proveedor);
                await _context.SaveChangesAsync();

                // 🔹 Registrar en bitácora
                var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value ?? "0");
                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = idUsuario,
                    Accion = "Activación de proveedor",
                    Descripcion = $"Proveedor '{proveedor.Nombre}' reactivado.",
                    Modulo = "Proveedores",
                    Fecha = FechaLocal.Ahora()
                });
                await _context.SaveChangesAsync();

                TempData["Success"] = "Proveedor activado exitosamente.";
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error al activar proveedor {Id}", id);
                TempData["Error"] = "Error al activar el proveedor.";
            }

            return RedirectToAction(nameof(Index));
        }

        // MÉTODO AUXILIAR PARA VALIDACIONES PERSONALIZADAS
        private async Task AplicarValidacionesPersonalizadas(Proveedor proveedor, int? idProveedorActual = null)
        {
            // Validar que el nombre no esté vacío
            if (string.IsNullOrWhiteSpace(proveedor.Nombre))
            {
                ModelState.AddModelError("Nombre", "El nombre del proveedor es obligatorio.");
            }

            // Validar formato de email si se proporciona
            if (!string.IsNullOrWhiteSpace(proveedor.Email))
            {
                var emailValidator = new EmailAddressAttribute();
                if (!emailValidator.IsValid(proveedor.Email))
                {
                    ModelState.AddModelError("Email", "El formato del email no es válido.");
                }
            }

            // Validar formato de teléfono si se proporciona
            if (!string.IsNullOrWhiteSpace(proveedor.Telefono))
            {
                // Validación básica de teléfono (solo números, guiones, espacios y paréntesis)
                if (!System.Text.RegularExpressions.Regex.IsMatch(proveedor.Telefono, @"^[\d\s\-\(\)\+]+$"))
                {
                    ModelState.AddModelError("Telefono", "El formato del teléfono no es válido. Solo se permiten números, espacios, guiones y paréntesis.");
                }
            }
        }

        // MÉTODO AUXILIAR PARA VERIFICAR EXISTENCIA
        private async Task<bool> ProveedorExists(int id)
        {
            return await _context.Proveedores.AnyAsync(e => e.IdProveedor == id);
        }
    }
}