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
            if (!ModelState.IsValid)
            {
                ViewData["Mensaje"] = "Datos inválidos";
                return View(modelo);
            }

            // Encriptar contraseña (si viene)
            if (!string.IsNullOrWhiteSpace(modelo.Clave))
                modelo.Clave = Utilidades.EncriptarClave(modelo.Clave);

            Usuario usuario_creado = await _usuarioServicio.SaveUsuario(modelo);

            if (usuario_creado != null && usuario_creado.IdUsuario > 0)
            {
                TempData["Success"] = "Usuario creado correctamente.";

                // Si quien creó es un admin (estás dentro de un Authorize admin, pero conservamos la comprobación)
                if (User?.Identity != null && User.Identity.IsAuthenticated && User.IsInRole("Administrador"))
                {
                    return RedirectToAction("Usuarios", "Admin");
                }

                // Flujo público (si en otro escenario se usa esta acción sin autenticación)
                return RedirectToAction("IniciarSesion", "Inicio");
            }

            ViewData["Mensaje"] = "No se pudo crear el usuario";
            return View(modelo);
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
            // Buscar al usuario en la base de datos comparando correo y clave encriptada.
            Usuario usuario_encontrado = await _usuarioServicio.GetUsuario(correo, Utilidades.EncriptarClave(clave));

            // Si no existe el usuario (no se encontró coincidencia en correo/clave), se muestra un mensaje de error.
            if (usuario_encontrado == null)
            {
                ViewData["Mensaje"] = "El Correo y/o Contraseña estan incorrectos";
                return View(); // vuelve a la vista de login.
            }

            // Claims con rol incluido
            List<Claim> claims = new List<Claim>() {
        new Claim(ClaimTypes.NameIdentifier, usuario_encontrado.IdUsuario.ToString()),
        new Claim(ClaimTypes.Name, usuario_encontrado.NombreUsuario),
        new Claim(ClaimTypes.Role, usuario_encontrado.Rol.NombreRol) //Ahora viene de la tabla Rol
            };

            

            
            // Crear la identidad del usuario usando los Claims y el esquema de autenticación por cookies.
            ClaimsIdentity claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);


            // Propiedades de la cookie de autenticación permitiendo renovar la cookie automaticamente por AllowRefresh = true.
            AuthenticationProperties properties = new AuthenticationProperties()
            {
                AllowRefresh = true
            };

            // Registrar al usuario en el sistema con autenticación basada en cookies. 
            await HttpContext.SignInAsync(

                CookieAuthenticationDefaults.AuthenticationScheme,   // Tipo de autenticación
                new ClaimsPrincipal(claimsIdentity),                 // Usuario con su identidad (Claims)
                properties                                           // Opciones adicionales
            );
  
            return RedirectToAction("Index", "Home");
        }
    }
}
