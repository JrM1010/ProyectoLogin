using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoLogin.Models;
using ProyectoLogin.Recursos;
using ProyectoLogin.Servicios.Contrato;
using System.Security.Claims;

namespace ProyectoLogin.Controllers
{
    public class InicioController : Controller
    {
        private readonly IUsuarioService _usuarioServicio;

        // Se recibe por inyección de dependencias un servicio que implementa IUsuarioService, usa logica del negocio sin acoplarse a la implementación concreta.
        public InicioController(IUsuarioService usuarioServicio)
        {
            _usuarioServicio = usuarioServicio;
        }

        [Authorize(Roles = "Administrador")]
        public IActionResult Registrarse() // Muestra el formulario de registro.
        {
            return View();
        }

        // Acción POST: se ejecuta cuando el usuario envía el formulario de registro. Recibe a Usuario con datos enviados de la vista.
        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Registrarse(Usuario modelo)
        {
            


            modelo.Clave = Utilidades.EncriptarClave(modelo.Clave); // Antes de guardar al usuario, se encripta la contra-
           
            Usuario usuario_creado = await _usuarioServicio.SaveUsuario(modelo); // Se llama al servicio de usuarios para guardar el nuevo usuario en la base de datos.

            if (usuario_creado.IdUsuario > 0) // Si el usuario fue creado correctamente, debería tener un Id mayor a 0.

                return RedirectToAction("IniciarSesion", "Inicio"); // Redirige a la acción "IniciarSesion" del controlador "Inicio".

            ViewData["Mensaje"] = "No se pudo crear el usuario"; // Si no se pudo crear el usuario, se muestra un mensaje de error en la vista.
            return View();
        }



        
        // Devuelve la vista vacía para que el usuario ingrese su correo y contraseña.
        public IActionResult IniciarSesion()
        {
            return View();
        }

        // Acción POST: se ejecuta cuando el usuario envía el formulario de inicio de sesión y recibe correo y clave desde el formulario.
        [HttpPost]
        public async Task<IActionResult> IniciarSesion(string correo, string clave)
        {
            try
            {
                Usuario usuario_encontrado = await _usuarioServicio.GetUsuario(correo, Utilidades.EncriptarClave(clave));

                if (usuario_encontrado == null)
                {
                    ViewData["Mensaje"] = "No se encontraron coincidencias";
                    return View();
                }

                // Verificar que el rol no sea nulo
                string nombreRol = usuario_encontrado.Rol?.NombreRol ?? "Usuario";

                List<Claim> claims = new List<Claim>() {
            new Claim(ClaimTypes.NameIdentifier, usuario_encontrado.IdUsuario.ToString()),
            new Claim(ClaimTypes.Name, usuario_encontrado.NombreUsuario ?? "Usuario"),
            new Claim(ClaimTypes.Role, nombreRol)
        };

                ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                AuthenticationProperties properties = new AuthenticationProperties()
                {
                    AllowRefresh = true,
                    IsPersistent = true
                };

                await HttpContext.SignInAsync(
                    CookieAuthenticationDefaults.AuthenticationScheme,
                    new ClaimsPrincipal(claimsIdentity),
                    properties
                );

                return RedirectToAction("Index", "Home");
            }
            catch (Exception ex)
            {
                ViewData["Mensaje"] = "Error durante el inicio de sesión";
                return View();
            }
        }
    }
}
