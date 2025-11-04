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
            // Cargar roles siempre antes de cualquier retorno de la vista
            ViewBag.Roles = await _context.Roles.OrderBy(r => r.IdRol).ToListAsync();

            // Validación manual para la contraseña en registro
            if (string.IsNullOrWhiteSpace(modelo.Clave))
            {
                ModelState.AddModelError("Clave", "La contraseña es obligatoria");
            }

            if (!ModelState.IsValid)
            {
                ViewData["Mensaje"] = "Por favor, corrija los errores en el formulario";
                return View(modelo);
            }

            try
            {
                // Encriptar la contraseña antes de guardar
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
            catch (DbUpdateException ex)
            {
                // Manejar errores de base de datos (como correos duplicados)
                ViewData["Mensaje"] = "Error al guardar el usuario. Verifique que el correo no esté en uso.";
                return View(modelo);
            }
            catch (Exception ex)
            {
                ViewData["Mensaje"] = "Ocurrió un error inesperado: " + ex.Message;
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