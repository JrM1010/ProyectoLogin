using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoLogin.Models;
using System.Diagnostics;
using System.Security.Claims;

namespace ProyectoLogin.Controllers
{

    
    //El authorize obliga a que el usuario esté autenticado para acceder a las acciones dentro del controlador.
    // Si no está autenticado, lo redirige automáticamente a la página de login.
    [Authorize]
    public class HomeController : Controller
    {
        // Inyección de dependencias de ILogger para registrar logs en el sistema.
        private readonly ILogger<HomeController> _logger;

        // Constructor del controlador.
        // Recibe un logger inyectado automáticamente por el framework.
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // Acción principal es la vista inicial después del login.
        public IActionResult Index()
        {
            // HttpContext.User devuelve el usuario autenticado actual (ClaimsPrincipal).
            ClaimsPrincipal claimuser = HttpContext.User;
            string nombreUsuario = "";

            
            if (claimuser.Identity.IsAuthenticated)
            {
                // Se obtiene el valor del Claim que guarda el nombre del usuario (ClaimTypes.Name).
                nombreUsuario = claimuser.Claims
                                         .Where(c => c.Type == ClaimTypes.Name)
                                         .Select(c => c.Value)
                                         .SingleOrDefault();
            }

            // Se pasa el nombre de usuario a la vista a través de ViewData.
            ViewData["nombreUsuario"] = nombreUsuario;

            return View();
        }



        // Acción Privacy devuelve la vista de política de privacidad.
        public IActionResult Privacy()
        {
            return View();
        }

        // Se ejecuta cuando ocurre un error en la aplicación.
        // ResponseCache evita que esta respuesta se almacene en caché.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            // Retorna una vista con un modelo que contiene el ID de la solicitud.
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> CerrarSesion()
        {
            // Elimina la cookie de autenticación, cerrando sesión del usuario.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Redirige al formulario de inicio de sesión en el controlador Inicio.
            return RedirectToAction("IniciarSesion", "Inicio");
        }
    }
}
