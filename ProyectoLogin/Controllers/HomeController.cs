using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using System.Diagnostics;
using System.Security.Claims;

namespace ProyectoLogin.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly ILogger<HomeController> _logger;
        private DbPruebaContext _context;

        public HomeController(ILogger<HomeController> logger, DbPruebaContext context)
        {
            _logger = logger;
            _context = context;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            var nombreUsuario = User.Identity?.Name ?? "Usuario";

            var productosEnStock = await _context.Inventarios.SumAsync(i => (int?)i.StockActual) ?? 0;
            var totalProductos = await _context.Productos.CountAsync(p => p.Activo);
            var totalClientes = await _context.Clientes.CountAsync(c => c.Activo);
            var totalUsuarios = await _context.Usuarios.CountAsync(u => u.Activo);
            var totalProveedores = await _context.Proveedores.CountAsync(p => p.Activo);

            var ultimasVentas = await _context.Ventas
                .Include(v => v.Cliente)
                .OrderByDescending(v => v.FechaVenta)
                .Take(5)
                .ToListAsync();

            var ultimasCompras = await _context.Compras
                .Include(c => c.Proveedor)
                .OrderByDescending(c => c.FechaCompra)
                .Take(5)
                .ToListAsync();

            ViewData["NombreUsuario"] = nombreUsuario;
            ViewData["ProductosEnStock"] = productosEnStock;
            ViewData["TotalProductos"] = totalProductos;
            ViewData["TotalClientes"] = totalClientes;
            ViewData["TotalUsuarios"] = totalUsuarios;
            ViewData["TotalProveedores"] = totalProveedores;
            ViewData["UltimasVentas"] = ultimasVentas;
            ViewData["UltimasCompras"] = ultimasCompras;

            return View();
        }


        public IActionResult Gestion()
        {
            return View();
        }

        public IActionResult Movimientos()
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