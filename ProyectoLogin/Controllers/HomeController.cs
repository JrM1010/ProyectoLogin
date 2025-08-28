using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoLogin.Models;
using System.Diagnostics;
using System.Security.Claims;

namespace ProyectoLogin.Controllers
{

    
    //El authorize obliga a que el usuario est� autenticado para acceder a las acciones dentro del controlador.
    // Si no est� autenticado, lo redirige autom�ticamente a la p�gina de login.
    [Authorize]
    public class HomeController : Controller
    {
        // Inyecci�n de dependencias de ILogger para registrar logs en el sistema.
        private readonly ILogger<HomeController> _logger;

        // Constructor del controlador.
        // Recibe un logger inyectado autom�ticamente por el framework.
        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        // Acci�n principal es la vista inicial despu�s del login.
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

            // Se pasa el nombre de usuario a la vista a trav�s de ViewData.
            ViewData["nombreUsuario"] = nombreUsuario;

            return View();
        }



        // Acci�n Privacy devuelve la vista de pol�tica de privacidad.
        public IActionResult Privacy()
        {
            return View();
        }

        // Se ejecuta cuando ocurre un error en la aplicaci�n.
        // ResponseCache evita que esta respuesta se almacene en cach�.
        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            // Retorna una vista con un modelo que contiene el ID de la solicitud.
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> CerrarSesion()
        {
            // Elimina la cookie de autenticaci�n, cerrando sesi�n del usuario.
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            // Redirige al formulario de inicio de sesi�n en el controlador Inicio.
            return RedirectToAction("IniciarSesion", "Inicio");
        }
    }
}
