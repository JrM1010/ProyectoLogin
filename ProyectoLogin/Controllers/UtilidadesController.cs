using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosProducts;
using ProyectoLogin.Recursos;
using System;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoLogin.Controllers
{
    [Authorize(Roles = "Administrador,Vendedor")]
    public class UtilidadesController : Controller  // ← Cambiado el nombre
    {
        private readonly DbPruebaContext _context;

        public UtilidadesController(DbPruebaContext context)
        {
            _context = context;
        }

        // GET: Vista principal de Utilidades (menú de opciones)
        public IActionResult Index()
        {
            return View();
        }

        // GET: Vista de Ajustes de Inventario
        public IActionResult AjustesInventario()  // ← Nueva acción
        {
            return View();
        }

        // GET: Buscar producto por código o nombre (AJAX)
        [HttpGet]
        public async Task<IActionResult> BuscarProducto(string term)
        {
            if (string.IsNullOrEmpty(term))
                return Json(new { success = false, message = "Término de búsqueda vacío" });

            var producto = await _context.Productos
                .Include(p => p.Inventario)
                .Include(p => p.Categoria)
                .Include(p => p.Marca)
                .Where(p => p.Activo &&
                           (p.CodigoBarras.Contains(term) || p.Nombre.Contains(term)))
                .Select(p => new
                {
                    id = p.IdProducto,
                    nombre = p.Nombre,
                    codigoBarras = p.CodigoBarras,
                    categoria = p.Categoria.Nombre,
                    marca = p.Marca.Nombre,
                    stockActual = p.Inventario != null ? p.Inventario.StockActual : 0,
                    stockMinimo = p.Inventario != null ? p.Inventario.StockMinimo : 0
                })
                .FirstOrDefaultAsync();

            if (producto == null)
                return Json(new { success = false, message = "Producto no encontrado" });

            return Json(new { success = true, producto });
        }

        // POST: Realizar ajuste de inventario
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AjustarStock([FromBody] AjusteStockRequest request)
        {
            if (!ModelState.IsValid)
                return Json(new { success = false, message = "Datos inválidos" });

            using var transaction = await _context.Database.BeginTransactionAsync();

            try
            {
                var producto = await _context.Productos
                    .Include(p => p.Inventario)
                    .FirstOrDefaultAsync(p => p.IdProducto == request.IdProducto && p.Activo);

                if (producto == null)
                    return Json(new { success = false, message = "Producto no encontrado" });

                var inventario = producto.Inventario;

                if (inventario == null)
                {
                    inventario = new Inventario
                    {
                        IdProducto = request.IdProducto,
                        StockActual = 0,
                        StockMinimo = 0,
                        FechaUltimaActualizacion = FechaLocal.Ahora()
                    };
                    _context.Inventarios.Add(inventario);
                    await _context.SaveChangesAsync();
                }

                int stockAnterior = inventario.StockActual;
                int nuevoStock;

                // 🔹 Aplicar ajuste a inventario
                if (request.TipoAjuste == "entrada")
                {
                    inventario.StockActual += request.Cantidad;
                    nuevoStock = stockAnterior + request.Cantidad;
                }
                else if (request.TipoAjuste == "salida")
                {
                    if (inventario.StockActual < request.Cantidad)
                    {
                        await transaction.RollbackAsync();
                        return Json(new
                        {
                            success = false,
                            message = $"Stock insuficiente. Stock actual: {inventario.StockActual}, solicitado: {request.Cantidad}"
                        });
                    }

                    inventario.StockActual -= request.Cantidad;
                    nuevoStock = stockAnterior - request.Cantidad;
                }
                else
                {
                    await transaction.RollbackAsync();
                    return Json(new { success = false, message = "Tipo de ajuste no válido" });
                }

                inventario.FechaUltimaActualizacion = FechaLocal.Ahora();

                // 🔹 Info de usuario
                var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                var nombreUsuario =
                    User.Identity?.Name
                    ?? User.FindFirst("Username")?.Value
                    ?? idUsuario.ToString();

                // 🔹 Cantidad con signo para el movimiento
                var cantidadMovimiento = request.TipoAjuste == "entrada"
                    ? request.Cantidad           // Entrada: positivo
                    : -request.Cantidad;         // Salida: negativo

                // 🔹 Registrar movimiento en MovInventario
                var movimiento = new MovInventario
                {
                    IdProducto = request.IdProducto,
                    Cantidad = cantidadMovimiento,
                    Fecha = FechaLocal.Ahora(),
                    TipoMovimiento = "Ajuste Manual",
                    Referencia = request.Motivo,
                    Observacion = $"Ajuste {request.TipoAjuste} de {request.Cantidad} unidades. " +
                                  $"Stock anterior: {stockAnterior}, nuevo stock: {nuevoStock}. Usuario: {nombreUsuario}",
                    UsuarioAjuste = nombreUsuario
                };

                _context.MovInventarios.Add(movimiento);
                await _context.SaveChangesAsync();

                // 🔹 Registrar en bitácora (igual que antes, pero usando las mismas vars)
                string tipoAccion = request.TipoAjuste == "entrada" ? "Ajuste de entrada" : "Ajuste de salida";

                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = idUsuario,
                    Accion = tipoAccion,
                    Descripcion = $"Producto ID {request.IdProducto}: {tipoAccion.ToLower()} de {request.Cantidad} unidades. Motivo: {request.Motivo}.",
                    Modulo = "Inventario",
                    Fecha = FechaLocal.Ahora()
                });

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return Json(new
                {
                    success = true,
                    message = $"Ajuste realizado correctamente. Nuevo stock: {nuevoStock}",
                    stockAnterior,
                    nuevoStock
                });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return Json(new { success = false, message = $"Error al realizar el ajuste: {ex.Message}" });
            }
        }



        // 🔹 Buscar productos por nombre o código (para autocompletado)
        [HttpGet]
        public async Task<IActionResult> SugerirProductos(string term)
        {
            if (string.IsNullOrWhiteSpace(term))
                return Json(new { success = false, productos = new object[0] });

            var productos = await _context.Productos
                .Include(p => p.Inventario)
                .Where(p => p.Activo &&
                            (p.Nombre.Contains(term) || p.CodigoBarras.Contains(term)))
                .Select(p => new
                {
                    id = p.IdProducto,
                    nombre = p.Nombre,
                    codigo = p.CodigoBarras,
                    stock = p.Inventario != null ? p.Inventario.StockActual : 0
                })
                .OrderBy(p => p.nombre)
                .Take(8)
                .ToListAsync();

            return Json(new { success = true, productos });
        }


        // GET: Historial de ajustes del producto
        [HttpGet]
        public async Task<IActionResult> ObtenerHistorial(int idProducto)
        {
            var movimientos = await _context.MovInventarios
                .Where(m => m.IdProducto == idProducto && m.TipoMovimiento == "Ajuste Manual")
                .OrderByDescending(m => m.Fecha)
                .Take(10)
                .Select(m => new
                {
                    fecha = m.Fecha.ToString("dd/MM/yyyy HH:mm"),
                    cantidad = m.Cantidad,
                    tipo = m.Cantidad > 0 ? "Entrada" : "Salida",
                    referencia = m.Referencia,
                    observacion = m.Observacion
                })
                .ToListAsync();

            return Json(new { success = true, movimientos });
        }
    }

    // Clase para el request del ajuste
    public class AjusteStockRequest
    {
        public int IdProducto { get; set; }
        public int Cantidad { get; set; }
        public string TipoAjuste { get; set; } // "entrada" o "salida"
        public string Motivo { get; set; }
    }
}