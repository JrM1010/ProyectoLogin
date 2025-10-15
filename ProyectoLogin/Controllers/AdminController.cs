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

    // ✅ LISTAR USUARIOS (activos e inactivos)
    public async Task<IActionResult> Usuarios()
    {
        var usuariosActivos = await _context.Usuarios
            .Include(u => u.Rol)
            .Where(u => u.Activo)
            .ToListAsync();

        var usuariosInactivos = await _context.Usuarios
            .Include(u => u.Rol)
            .Where(u => !u.Activo)
            .ToListAsync();

        ViewBag.UsuariosInactivos = usuariosInactivos;
        return View(usuariosActivos);
    }

    // ✅ DESACTIVAR USUARIO (soft delete)
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

    // ✅ REACTIVAR USUARIO
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

    // 🔧 Editar usuario (sin cambios)
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();

        ViewBag.Roles = await _context.Roles.ToListAsync();
        return View(usuario);
    }

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
