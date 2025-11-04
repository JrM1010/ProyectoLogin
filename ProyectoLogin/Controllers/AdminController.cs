using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;

[Authorize(Roles = "Administrador")]
public class AdminController : Controller
{
    private readonly DbPruebaContext _context;

    public AdminController(DbPruebaContext context)
    {
        _context = context;
    }

    // Listar usuarios (activos e inactivos) con paginación
    public async Task<IActionResult> Usuarios(string q, int pageActivos = 1, int pageInactivos = 1, int pageSize = 5)
    {
        var usuariosQuery = _context.Usuarios
            .Include(u => u.Rol)
            .AsQueryable();

        if (!string.IsNullOrEmpty(q))
        {
            usuariosQuery = usuariosQuery.Where(u =>
                u.NombreUsuario.Contains(q) ||
                u.Correo.Contains(q));
            ViewData["q"] = q;
        }

        // Separar activos e inactivos
        var usuariosActivos = await usuariosQuery
            .Where(u => u.Activo)
            .OrderBy(u => u.NombreUsuario)
            .ToListAsync();

        var usuariosInactivos = await usuariosQuery
            .Where(u => !u.Activo)
            .OrderBy(u => u.NombreUsuario)
            .ToListAsync();

        // Aplicar paginación independiente
        var activosPaginados = usuariosActivos.Skip((pageActivos - 1) * pageSize).Take(pageSize).ToList();
        var inactivosPaginados = usuariosInactivos.Skip((pageInactivos - 1) * pageSize).Take(pageSize).ToList();

        // Calcular total de páginas
        ViewBag.TotalActivos = usuariosActivos.Count;
        ViewBag.TotalInactivos = usuariosInactivos.Count;
        ViewBag.PageSize = pageSize;
        ViewBag.PageActivos = pageActivos;
        ViewBag.PageInactivos = pageInactivos;
        ViewBag.TotalPagesActivos = (int)Math.Ceiling(usuariosActivos.Count / (double)pageSize);
        ViewBag.TotalPagesInactivos = (int)Math.Ceiling(usuariosInactivos.Count / (double)pageSize);

        // Pasar las listas paginadas a la vista
        ViewBag.Activos = activosPaginados;
        ViewBag.Inactivos = inactivosPaginados;

        return View(usuariosActivos);
    }

    // Desactivar usuarios (soft delete)
    [HttpPost]
    public async Task<IActionResult> Desactivar(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario != null)
        {
            usuario.Activo = false;
            _context.Update(usuario);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction("Usuarios");
    }

    // Reactivar usuarios
    [HttpPost]
    public async Task<IActionResult> Activar(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario != null)
        {
            usuario.Activo = true;
            _context.Update(usuario);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction("Usuarios");
    }

    // Editar usuario (sin cambios)
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();

        ViewBag.Roles = await _context.Roles.ToListAsync();
        return View(usuario);
    }


    // Editar usuario (cambios en rol)
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Editar(Usuario model)
    {
        var usuario = await _context.Usuarios.FindAsync(model.IdUsuario);
        if (usuario == null) return NotFound();

        if (model.IdRol == 0)
        {
            ModelState.AddModelError("IdRol", "Debe seleccionar un rol válido.");
            ViewBag.Roles = await _context.Roles.ToListAsync();
            return View(model);
        }

        usuario.IdRol = model.IdRol;
        _context.Update(usuario);
        await _context.SaveChangesAsync();

        return RedirectToAction("Usuarios");
    }
}
