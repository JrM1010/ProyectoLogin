using Microsoft.AspNetCore.Mvc;

using ProyectoLogin.Models;
using ProyectoLogin.Recursos;
using ProyectoLogin.Servicios.Contrato;

using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication;

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

        
        public IActionResult Registrarse() // Muestra el formulario de registro.
        {
            return View();
        }

        // Acción POST: se ejecuta cuando el usuario envía el formulario de registro. Recibe a Usuario con datos enviados de la vista.
        [HttpPost]
        public async Task<IActionResult> Registrarse(Usuario modelo)
        {
            
            modelo.Clave = Utilidades.EncriptarClave(modelo.Clave); // Antes de guardar al usuario, se encripta la contra-
           
            Usuario usuario_creado = await _usuarioServicio.SaveUsuario(modelo); // Se llama al servicio de usuarios para guardar el nuevo usuario en la base de datos.

            if (usuario_creado.IdUsuario > 0) // Si el usuario fue creado correctamente, debería tener un Id mayor a 0.

                return RedirectToAction("IniciarSesion", "Inicio"); // Redirige a la acción "IniciarSesion" del controlador "Inicio".

            ViewData["Mensaje"] = "No se pudo crear el usu+ario"; // Si no se pudo crear el usuario, se muestra un mensaje de error en la vista.
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
            // Buscar al usuario en la base de datos comparando correo y clave encriptada.
            Usuario usuario_encontrado = await _usuarioServicio.GetUsuario(correo, Utilidades.EncriptarClave(clave));

            // Si no existe el usuario (no se encontró coincidencia en correo/clave), se muestra un mensaje de error.
            if (usuario_encontrado == null)
            {
                ViewData["Mensaje"] = "No se encontraron coincidencias ";
                return View(); // vuelve a la vista de login.
            }

            // Claims con rol incluido
            List<Claim> claims = new List<Claim>() {
        new Claim(ClaimTypes.Name, usuario_encontrado.NombreUsuario),
        new Claim(ClaimTypes.Role, usuario_encontrado.Rol.NombreRol) // ✅ Ahora viene de la tabla Rol
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
