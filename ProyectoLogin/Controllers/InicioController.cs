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
            if (!ModelState.IsValid)
            {
                ViewData["Mensaje"] = "Datos inválidos";
                return View(modelo);
            }

            if (!string.IsNullOrWhiteSpace(modelo.Clave))
                modelo.Clave = Utilidades.EncriptarClave(modelo.Clave);

            Usuario usuario_creado = await _usuarioServicio.SaveUsuario(modelo);

            if (usuario_creado != null && usuario_creado.IdUsuario > 0)
            {
                TempData["Success"] = "Usuario creado correctamente.";

                if (User?.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Administrador"))
                {
                    return RedirectToAction("Usuarios", "Admin");
                }

                return RedirectToAction("IniciarSesion", "Inicio");
            }

            ViewData["Mensaje"] = "No se pudo crear el usuario";
            return View(modelo);
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
                ViewData["Mensaje"] = "El Correo y/o Contraseña estan incorrectos";
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
    }
}