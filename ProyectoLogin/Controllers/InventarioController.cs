using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using System.Linq;

namespace ProyectoLogin.Controllers
{
    [Authorize(Roles = "Administrador,Gerente")]
    public class InventarioController : Controller
    {
        private readonly DbPruebaContext _context;

        public InventarioController(DbPruebaContext context)
        {
            _context = context;
        }

        // GET: Inventario
        public async Task<IActionResult> Index(string searchString, int? categoriaId, int? marcaId)
        {
            var productos = _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Marca)
                .Include(p => p.Proveedor)
                .Where(p => p.Activo);

            if (!string.IsNullOrEmpty(searchString))
            {
                productos = productos.Where(p =>
                    p.Nombre.Contains(searchString) ||
                    p.Descripcion.Contains(searchString) ||
                    p.CodigoBarras.Contains(searchString));
            }

            if (categoriaId.HasValue)
            {
                productos = productos.Where(p => p.IdCategoria == categoriaId);
            }

            if (marcaId.HasValue)
            {
                productos = productos.Where(p => p.IdMarca == marcaId);
            }

            // CONVERTIR a SelectListItem
            ViewBag.Categorias = _context.Categorias
                .Where(c => c.Activo)
                .Select(c => new SelectListItem
                {
                    Value = c.IdCategoria.ToString(),
                    Text = c.Nombre
                })
                .ToList();

            ViewBag.Marcas = _context.Marcas
                .Where(m => m.Activo)
                .Select(m => new SelectListItem
                {
                    Value = m.IdMarca.ToString(),
                    Text = m.Nombre
                })
                .ToList();

            return View(await productos.ToListAsync());
        }




        // GET: Buscar productos (para vendedores)
        [Authorize(Roles = "Vendedor,Administrador,Gerente")]
        public async Task<IActionResult> Buscar(string term)
        {
            var productos = await _context.Productos
                    .Where(p => p.Activo &&
            (p.Nombre.Contains(term) || p.CodigoBarras.Contains(term)))
                    .Select(p => new {
            id = p.IdProducto,
            text = p.Nombre,
            precio = p.PrecioVenta,
            stock = p.Stock,
            stockMinimo = p.StockMinimo
        })
        .Take(10)
        .ToListAsync();

            return Json(productos);
        }





        // GET: Detalles del producto
        [Authorize(Roles = "Vendedor,Administrador,Gerente")]
        public async Task<IActionResult> Detalles(int id)
        {
            var producto = await _context.Productos
                .Include(p => p.Categoria)
                .Include(p => p.Marca)
                .Include(p => p.Proveedor)
                .FirstOrDefaultAsync(p => p.IdProducto == id);

            if (producto == null)
            {
                return NotFound();
            }

            return View("Detalles", producto);
        }
    }
}
