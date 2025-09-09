using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;

[Authorize(Roles = "Administrador")] //Solo admins
public class AdminController : Controller
{
    private readonly DbpruebaContext _context;

    public AdminController(DbpruebaContext context)
    {
        _context = context;
    }

    //Listar usuarios
    public async Task<IActionResult> Usuarios()
    {
        var usuarios = await _context.Usuarios.Include(u => u.Rol).ToListAsync();
        return View(usuarios);
    }

    //Eliminar usuario
    [HttpPost]
    public async Task<IActionResult> Eliminar(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario != null)
        {
            _context.Usuarios.Remove(usuario);
            await _context.SaveChangesAsync();
        }
        return RedirectToAction("Usuarios");
    }

    //Editar usuario (GET)
    [HttpGet]
    public async Task<IActionResult> Editar(int id)
    {
        var usuario = await _context.Usuarios.FindAsync(id);
        if (usuario == null) return NotFound();

        // Pasar lista de roles a la vista
        ViewBag.Roles = await _context.Roles.ToListAsync();
        return View(usuario);
    }

    //Editar usuario (POST)
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

        // Aquí sí se actualiza el rol
        usuario.IdRol = model.IdRol;
        _context.Update(usuario);
        await _context.SaveChangesAsync();

        return RedirectToAction("Usuarios");
    }





}

