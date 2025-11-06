using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosProducts; // ProductoCore
using ProyectoLogin.Models.Promociones;
using ProyectoLogin.Recursos;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace ProyectoLogin.Controllers
{
    // Ajusta roles según tu necesidad; si cualquiera puede crear kits quita el Authorize
    [Authorize(Roles = "Administrador, Vendedor")]
    public class KitsController : Controller
    {
        private readonly DbPruebaContext _context;

        public KitsController(DbPruebaContext context)
        {
            _context = context;
        }

        // GET: /Kits
        public async Task<IActionResult> Index()
        {
            // Lista de kits con su total
            var kits = await _context.Kits
                .Where(k => k.Activo)
                .OrderByDescending(k => k.FechaCreacion)
                .ToListAsync();

            return View(kits);
        }

        [Authorize(Roles = "Administrador")]
        // GET: /Kits/Create
        public async Task<IActionResult> Create()
        {
            // Traer productos activos con precio de venta actual (último activo)
            var productos = await _context.Productos
                .Where(p => p.Activo)
                .Select(p => new
                {
                    p.IdProducto,
                    p.Nombre,
                    PrecioVenta = _context.ProductoPrecio
                        .Where(pp => pp.IdProducto == p.IdProducto && pp.Activo)
                        .OrderByDescending(pp => pp.FechaInicio)
                        .Select(pp => pp.PrecioVenta)
                        .FirstOrDefault()
                })
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            ViewBag.Productos = productos;
            return View();
        }

        [Authorize(Roles = "Administrador")]
        // POST: /Kits/CalcularPrecio
        // Devuelve subtotal, descuento y total — usa precios actuales desde DB (no confía en el cliente)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CalcularPrecio([FromBody] CalcularRequest request)
        {
            if (request == null || request.Items == null || !request.Items.Any())
                return BadRequest(new { success = false, message = "Items inválidos." });

            var productoIds = request.Items.Select(i => i.IdProducto).Distinct().ToList();

            // Obtener precio activo más reciente por producto
            var precios = await _context.ProductoPrecio
                .Where(pp => productoIds.Contains(pp.IdProducto) && pp.Activo)
                .GroupBy(pp => pp.IdProducto)
                .Select(g => new
                {
                    IdProducto = g.Key,
                    Precio = g.OrderByDescending(x => x.FechaInicio).FirstOrDefault().PrecioVenta
                })
                .ToListAsync();

            decimal subtotal = 0m;
            var detalles = new List<object>();

            foreach (var it in request.Items)
            {
                var p = precios.FirstOrDefault(x => x.IdProducto == it.IdProducto);
                decimal precio = p?.Precio ?? 0m;
                decimal lineSubtotal = precio * it.Cantidad;
                subtotal += lineSubtotal;

                detalles.Add(new
                {
                    it.IdProducto,
                    it.Cantidad,
                    PrecioUnitario = precio,
                    Subtotal = lineSubtotal
                });
            }

            decimal descuentoPct = request.DescuentoPct <= 0 ? 0m : request.DescuentoPct;
            if (descuentoPct > 1m && descuentoPct <= 100m) // si vienen % en 2 en vez de 0.02
            {
                descuentoPct = descuentoPct / 100m;
            }

            decimal descuento = subtotal * descuentoPct;
            decimal total = subtotal - descuento;

            return Ok(new
            {
                success = true,
                subtotal,
                descuento,
                total,
                detalles
            });
        }

        [Authorize(Roles = "Administrador")]
        // POST: /Kits/Create
        // Guarda el Kit y sus detalles (usa los precios actuales y los almacena como snapshot)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([FromBody] CrearKitRequest request)
        {
            if (request == null || string.IsNullOrWhiteSpace(request.Nombre))
                return BadRequest(new { success = false, message = "Datos del kit inválidos." });

            if (request.Items == null || !request.Items.Any())
                return BadRequest(new { success = false, message = "Agrega al menos un producto al kit." });

            // Normalizar descuento
            decimal descuentoPct = request.DescuentoPct <= 0 ? 0m : request.DescuentoPct;
            if (descuentoPct > 1m && descuentoPct <= 100m) descuentoPct = descuentoPct / 100m;

            var productoIds = request.Items.Select(i => i.IdProducto).Distinct().ToList();

            // Cargar precios activos (snapshot) desde BD para evitar manipulacion por cliente
            var precios = await _context.ProductoPrecio
                .Where(pp => productoIds.Contains(pp.IdProducto) && pp.Activo)
                .GroupBy(pp => pp.IdProducto)
                .Select(g => new
                {
                    IdProducto = g.Key,
                    Precio = g.OrderByDescending(x => x.FechaInicio).FirstOrDefault().PrecioVenta
                })
                .ToListAsync();

            decimal subtotal = 0m;
            var detallesAGuardar = new List<KitDetalle>();

            foreach (var it in request.Items)
            {
                var p = precios.FirstOrDefault(x => x.IdProducto == it.IdProducto);
                decimal precioActual = p?.Precio ?? 0m;

                if (precioActual <= 0m)
                {
                    // Decide si quieres impedir guardar o permitir con precio 0; aquí bloqueamos.
                    return BadRequest(new { success = false, message = $"El producto {it.IdProducto} no tiene precio de venta activo." });
                }

                var kd = new KitDetalle
                {
                    IdProducto = it.IdProducto,
                    Cantidad = it.Cantidad,
                    PrecioUnitarioSnapshot = precioActual
                };

                detallesAGuardar.Add(kd);
                subtotal += precioActual * it.Cantidad;
            }

            decimal descuento = subtotal * descuentoPct;
            decimal total = subtotal - descuento;

            var kit = new Kit
            {
                Nombre = request.Nombre,
                Descripcion = request.Descripcion,
                Subtotal = subtotal,
                DescuentoPct = descuentoPct,
                Total = total,
                Activo = true,
                FechaCreacion = FechaLocal.Ahora()
            };

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.Kits.Add(kit);
                await _context.SaveChangesAsync();

                // asociar IdKit y guardar detalles
                foreach (var d in detallesAGuardar)
                {
                    d.IdKit = kit.IdKit;
                    _context.KitDetalles.Add(d);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // 🔹 Registrar movimiento en bitácora
                var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);

                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = idUsuario,
                    Accion = "Creación de kit",
                    Descripcion = $"Se creó el kit '{kit.Nombre}'. Total: Q{kit.Total:F2}.",
                    Modulo = "Kits",
                    Fecha = FechaLocal.Ahora()
                });

                await _context.SaveChangesAsync();


                return Ok(new { success = true, idKit = kit.IdKit });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Error guardando kit: " + ex.Message });
            }
        }
        [Authorize(Roles = "Administrador, Vendedor")]
        // GET: /Kits/Details/5
        public async Task<IActionResult> Details(int id)
        {
            var kit = await _context.Kits
                .Include(k => k.Detalles!)
                    .ThenInclude(d => d.Producto)
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.IdKit == id);

            if (kit == null) return NotFound();

            return View(kit);
        }


        [Authorize(Roles = "Administrador")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var kit = await _context.Kits.FindAsync(id);
            if (kit == null)
                return NotFound();

            // 🔹 Soft delete
            kit.Activo = false;
            _context.Kits.Update(kit);
            await _context.SaveChangesAsync();

            // 🔹 Registrar movimiento en bitácora
            var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
            _context.BitacoraMovimientos.Add(new BitacoraMovimiento
            {
                IdUsuario = idUsuario,
                Accion = "Desactivación de kit",
                Descripcion = $"Se desactivó el kit '{kit.Nombre}'.",
                Modulo = "Kits",
                Fecha = FechaLocal.Ahora()
            });
            await _context.SaveChangesAsync();

            return Ok(new { success = true });
        }

        [Authorize(Roles = "Administrador")]
        // GET: /Kits/Edit/5
        public async Task<IActionResult> Edit(int id)
        {
            var kit = await _context.Kits
                .Include(k => k.Detalles!)
                    .ThenInclude(d => d.Producto)
                .AsNoTracking()
                .FirstOrDefaultAsync(k => k.IdKit == id);

            if (kit == null) return NotFound();

            // Traer productos activos y su precio de venta actual (igual que en Create)
            var productos = await _context.Productos
                .Where(p => p.Activo)
                .Select(p => new
                {
                    p.IdProducto,
                    p.Nombre,
                    PrecioVenta = _context.ProductoPrecio
                        .Where(pp => pp.IdProducto == p.IdProducto && pp.Activo)
                        .OrderByDescending(pp => pp.FechaInicio)
                        .Select(pp => pp.PrecioVenta)
                        .FirstOrDefault()
                })
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            ViewBag.Productos = productos;
            return View(kit);
        }

        [Authorize(Roles = "Administrador")]
        // POST: /Kits/Edit
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit([FromBody] EditKitRequest request)
        {
            if (request == null || request.IdKit <= 0 || string.IsNullOrWhiteSpace(request.Nombre))
                return BadRequest(new { success = false, message = "Datos inválidos." });

            if (request.Items == null || !request.Items.Any())
                return BadRequest(new { success = false, message = "Agrega al menos un producto al kit." });

            // Normalizar descuento
            decimal descuentoPct = request.DescuentoPct <= 0 ? 0m : request.DescuentoPct;
            if (descuentoPct > 1m && descuentoPct <= 100m) descuentoPct = descuentoPct / 100m;

            var kit = await _context.Kits
                .Include(k => k.Detalles)
                .FirstOrDefaultAsync(k => k.IdKit == request.IdKit);

            if (kit == null) return NotFound(new { success = false, message = "Kit no encontrado." });

            var productoIds = request.Items.Select(i => i.IdProducto).Distinct().ToList();

            // Obtener precios snapshot (precio activo más reciente)
            var precios = await _context.ProductoPrecio
                .Where(pp => productoIds.Contains(pp.IdProducto) && pp.Activo)
                .GroupBy(pp => pp.IdProducto)
                .Select(g => new
                {
                    IdProducto = g.Key,
                    Precio = g.OrderByDescending(x => x.FechaInicio).FirstOrDefault().PrecioVenta
                })
                .ToListAsync();

            decimal subtotal = 0m;
            var nuevosDetalles = new List<KitDetalle>();

            foreach (var it in request.Items)
            {
                var p = precios.FirstOrDefault(x => x.IdProducto == it.IdProducto);
                decimal precioActual = p?.Precio ?? 0m;

                if (precioActual <= 0m)
                {
                    return BadRequest(new { success = false, message = $"El producto {it.IdProducto} no tiene precio de venta activo." });
                }

                var kd = new KitDetalle
                {
                    IdProducto = it.IdProducto,
                    Cantidad = it.Cantidad,
                    PrecioUnitarioSnapshot = precioActual,
                    IdKit = kit.IdKit // se setea antes de guardar
                };

                nuevosDetalles.Add(kd);
                subtotal += precioActual * it.Cantidad;
            }

            decimal descuento = subtotal * descuentoPct;
            decimal total = subtotal - descuento;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Eliminar detalles antiguos
                if (kit.Detalles != null && kit.Detalles.Any())
                {
                    _context.KitDetalles.RemoveRange(kit.Detalles);
                    await _context.SaveChangesAsync();
                }

                // Actualizar datos del kit
                kit.Nombre = request.Nombre;
                kit.Descripcion = request.Descripcion;
                kit.Subtotal = subtotal;
                kit.DescuentoPct = descuentoPct;
                kit.Total = total;
                kit.Activo = request.Activo; // si lo incluyes
                                             // Si tienes campo FechaModificacion: kit.FechaModificacion = FechaLocal.Ahora();

                _context.Kits.Update(kit);
                await _context.SaveChangesAsync();

                // Agregar nuevos detalles
                foreach (var d in nuevosDetalles)
                {
                    d.IdKit = kit.IdKit;
                    _context.KitDetalles.Add(d);
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Registrar bitácora
                var idUsuario = int.Parse(User.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier).Value);
                _context.BitacoraMovimientos.Add(new BitacoraMovimiento
                {
                    IdUsuario = idUsuario,
                    Accion = "Edición de kit",
                    Descripcion = $"Se editó el kit '{kit.Nombre}'. Ahora tiene {nuevosDetalles.Count} productos. Total: Q{kit.Total:F2}.",
                    Modulo = "Kits",
                    Fecha = FechaLocal.Ahora()
                });

                await _context.SaveChangesAsync();

                return Ok(new { success = true, idKit = kit.IdKit });
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                return BadRequest(new { success = false, message = "Error actualizando kit: " + ex.Message });
            }
        }

        #region DTOs adicionales para Edit
        public class EditKitRequest
        {
            public int IdKit { get; set; }
            public string Nombre { get; set; } = null!;
            public string? Descripcion { get; set; }
            public bool Activo { get; set; } = true;
            public decimal DescuentoPct { get; set; } = 0m;
            public List<ItemRequest> Items { get; set; } = new List<ItemRequest>();
        }
        #endregion


        #region DTOs

        // Request para calcular precio (preview)
        public class CalcularRequest
        {
            public List<ItemRequest> Items { get; set; } = new List<ItemRequest>();
            public decimal DescuentoPct { get; set; } = 0m; // aceptar 2 o 0.02
        }

        // Request para crear kit
        public class CrearKitRequest
        {
            [Required(ErrorMessage = "El nombre del kit es obligatorio.")]
            [StringLength(100, ErrorMessage = "El nombre no puede exceder los 100 caracteres.")]
            public string Nombre { get; set; } = null!;

            [Required(ErrorMessage = "La descripción del kit es obligatoria.")]
            [StringLength(300, ErrorMessage = "La descripción no puede exceder los 300 caracteres.")]
            public string? Descripcion { get; set; }
            public decimal DescuentoPct { get; set; } = 0m;
            public List<ItemRequest> Items { get; set; } = new List<ItemRequest>();
        }

        public class ItemRequest
        {
            public int IdProducto { get; set; }
            public int Cantidad { get; set; } = 1;
        }

        #endregion
    }
}

