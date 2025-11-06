using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Recursos;
using ProyectoLogin.Servicios.Contrato;
using System.Security.Claims;

namespace ProyectoLogin.Controllers
{
    public class InicioController : Controller
    {
        private readonly IUsuarioService _usuarioServicio;
        private readonly DbPruebaContext _context;

        public InicioController(IUsuarioService usuarioServicio, DbPruebaContext context)
        {
            _usuarioServicio = usuarioServicio;
            _context = context;
        }

        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> RegistrarseAsync()
        {
            ViewBag.Roles = await _context.Roles.OrderBy(r => r.IdRol).ToListAsync();
            return View();
        }


        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrarse(Usuario modelo)
        {
            ViewBag.Roles = await _context.Roles.OrderBy(r => r.IdRol).ToListAsync();

            if (!ModelState.IsValid)
            {
                ViewData["Mensaje"] = "Por favor, corrija los errores en el formulario";
                return View(modelo);
            }

            try
            {
                // Crear nuevo usuario
                var nuevoUsuario = new Usuario
                {
                    NombreUsuario = modelo.NombreUsuario.Trim(),
                    Correo = modelo.Correo.Trim(),
                    Clave = Utilidades.EncriptarClave(modelo.Clave),
                    IdRol = modelo.IdRol,
                    Activo = true
                };

                _context.Usuarios.Add(nuevoUsuario);
                await _context.SaveChangesAsync();

                TempData["Success"] = "Usuario creado correctamente.";
                return RedirectToAction("Usuarios", "Admin");
            }
            catch (Exception ex)
            {
                ViewData["Mensaje"] = "Error al crear el usuario: " + ex.Message;
                return View(modelo);
            }
        }

        public IActionResult IniciarSesion()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> IniciarSesion(string correo, string clave)
        {
            Usuario usuario_encontrado = await _usuarioServicio.GetUsuario(correo, Utilidades.EncriptarClave(clave));

            if (usuario_encontrado == null)
            {
                ViewData["Mensaje"] = "El Correo y/o Contraseña están incorrectos";
                return View();
            }

            List<Claim> claims = new List<Claim>() {
                new Claim(ClaimTypes.NameIdentifier, usuario_encontrado.IdUsuario.ToString()),
                new Claim(ClaimTypes.Name, usuario_encontrado.NombreUsuario),
                new Claim(ClaimTypes.Role, usuario_encontrado.Rol.NombreRol)
            };

            ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);

            AuthenticationProperties properties = new AuthenticationProperties()
            {
                AllowRefresh = true
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                properties
            );

            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> CerrarSesion()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("IniciarSesion", "Inicio");
        }
    }
}