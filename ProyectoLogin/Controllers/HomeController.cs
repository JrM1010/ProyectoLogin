using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ProyectoLogin.Models;
using System.Diagnostics;
using System.Security.Claims;

namespace ProyectoLogin.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;

        public HomeController(ILogger<HomeController> logger)
        {
            _logger = logger;
        }

        public IActionResult Index()
        {
            try
            {
                ClaimsPrincipal claimuser = HttpContext.User;
                string nombreUsuario = "Usuario";

                if (claimuser?.Identity?.IsAuthenticated == true)
                {
                    // Buscar el claim de nombre de diferentes maneras
                    var nameClaim = claimuser.Claims
                        .FirstOrDefault(c => c.Type == ClaimTypes.Name ||
                                           c.Type == "name" ||
                                           c.Type == "http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name");

                    if (nameClaim != null)
                    {
                        nombreUsuario = nameClaim.Value;
                    }
                    else
                    {
                        // Si no encuentra el claim, usar el nombre del Identity
                        nombreUsuario = claimuser.Identity.Name ?? "Usuario";
                    }
                }

                ViewData["nombreUsuario"] = nombreUsuario;
                return View();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error en Home/Index");
                ViewData["nombreUsuario"] = "Usuario";
                return View();
            }
        }

        public IActionResult Privacy()
        {
            return View();
        }

        [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
        public IActionResult Error()
        {
            return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
        }

        public async Task<IActionResult> CerrarSesion()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("IniciarSesion", "Inicio");
        }
    }
}