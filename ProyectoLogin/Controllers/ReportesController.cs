using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosVentas;
using ProyectoLogin.Recursos;
using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Previewer;

namespace ProyectoLogin.Controllers
{
    public class ReportesController : Controller
    {
        private readonly DbPruebaContext _context;
        
        public ReportesController(DbPruebaContext context)
        {
            _context = context;
        }

        public IActionResult Index()
        {
            var reportes = new List<string> { "Reporte de Ventas" };
            return View(reportes);
        }

        // 🔹 Filtro de ventas
        public async Task<IActionResult> Ventas(DateTime? desde, DateTime? hasta, string vendedor, string formaPago)
        {
            var query = _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.Usuario)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Kit)
                .AsQueryable();

            // 🔹 CONVERTIR FECHAS A UTC PARA COMPARACIÓN CORRECTA
            if (desde.HasValue)
            {
                var desdeUtc = FechaLocal.ConvertirAUtc(desde.Value);
                query = query.Where(v => v.FechaVenta >= desdeUtc);
            }

            if (hasta.HasValue)
            {
                // 🔹 AGREGAR 1 DÍA PARA INCLUIR EL DÍA COMPLETO
                var hastaUtc = FechaLocal.ConvertirAUtc(hasta.Value.AddDays(1));
                query = query.Where(v => v.FechaVenta < hastaUtc);
            }

            if (!string.IsNullOrEmpty(vendedor))
                query = query.Where(v => v.Usuario.NombreUsuario.Contains(vendedor));

            if (!string.IsNullOrEmpty(formaPago))
                query = query.Where(v => v.MetodoPago == formaPago);

            var ventas = await query
            .OrderByDescending(v => v.FechaVenta)
            .Select(v => new
            {
                IdVenta = v.IdVenta,
                Fecha = FechaLocal.ConvertirDeUtc(v.FechaVenta),
                NoFactura = v.NumeroFactura,
                Cliente = (string.IsNullOrWhiteSpace(v.Cliente.Nombres) && string.IsNullOrWhiteSpace(v.Cliente.Apellidos))
                            ? "CF"
                            : (v.Cliente.Nombres + " " + v.Cliente.Apellidos),
                Nit = string.IsNullOrWhiteSpace(v.Cliente.Nit) ? "CF" : v.Cliente.Nit,
                Vendedor = v.Usuario.NombreUsuario,
                TotalSinIva = v.Total / 1.12m,
                ValorIva = v.Total - (v.Total / 1.12m),
                TotalConIva = v.Total,
                FormaPago = v.MetodoPago,

                // 🧮 NUEVO: total de utilidad por venta
                UtilidadTotal = v.Detalles.Sum(d => d.Utilidad),

                // 🔹 INCLUIR DETALLES DE LA VENTA
                Detalles = v.Detalles.Select(d => new
                {
                    IdDetalle = d.IdDetalleVenta,
                    IdProducto = d.IdProducto,
                    IdKit = d.IdKit,
                    NombreProducto = d.Producto != null ? d.Producto.Nombre :
                                    d.Kit != null ? "(KIT) " + d.Kit.Nombre : "Producto no disponible",
                    Cantidad = d.Cantidad,
                    PrecioUnitario = d.PrecioUnitario,
                    Subtotal = d.Subtotal,
                    PrecioSinIva = d.PrecioUnitario / 1.12m,
                    SubtotalSinIva = d.Subtotal / 1.12m,
                    Iva = d.Subtotal - (d.Subtotal / 1.12m),
                    EsKit = d.IdKit.HasValue,

                    // 🧩 NUEVO: utilidad por producto
                    Utilidad = d.Utilidad
                }).ToList()
            })
            .ToListAsync();


            // Calcular totales
            ViewBag.UtilidadGeneral = ventas.Sum(v => v.UtilidadTotal);
            ViewBag.SubtotalGeneral = ventas.Sum(v => v.TotalSinIva);
            ViewBag.IvaGeneral = ventas.Sum(v => v.ValorIva);
            ViewBag.TotalGeneral = ventas.Sum(v => v.TotalConIva);

            return View("ReporteVentas", ventas);
        }

        [HttpGet]
        public async Task<IActionResult> DescargarPDF(DateTime? desde, DateTime? hasta, string vendedor, string formaPago)
        {
            var query = _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.Usuario)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Kit)
                .AsQueryable();

            // Filtros
            if (desde.HasValue)
            {
                var desdeUtc = FechaLocal.ConvertirAUtc(desde.Value);
                query = query.Where(v => v.FechaVenta >= desdeUtc);
            }
            if (hasta.HasValue)
            {
                var hastaUtc = FechaLocal.ConvertirAUtc(hasta.Value.AddDays(1));
                query = query.Where(v => v.FechaVenta < hastaUtc);
            }
            if (!string.IsNullOrEmpty(vendedor))
                query = query.Where(v => v.Usuario.NombreUsuario.Contains(vendedor));
            if (!string.IsNullOrEmpty(formaPago))
                query = query.Where(v => v.MetodoPago == formaPago);

            // Obtener datos
            var ventas = await query
                .OrderByDescending(v => v.FechaVenta)
                .Select(v => new
                {
                    Fecha = FechaLocal.ConvertirDeUtc(v.FechaVenta),
                    NoFactura = v.NumeroFactura,
                    Cliente = (string.IsNullOrWhiteSpace(v.Cliente.Nombres) && string.IsNullOrWhiteSpace(v.Cliente.Apellidos))
                                ? "CF"
                                : (v.Cliente.Nombres + " " + v.Cliente.Apellidos),
                    Nit = string.IsNullOrWhiteSpace(v.Cliente.Nit) ? "CF" : v.Cliente.Nit,
                    Vendedor = v.Usuario.NombreUsuario,
                    FormaPago = v.MetodoPago,
                    Subtotal = v.Total / 1.12m,
                    Iva = v.Total - (v.Total / 1.12m),
                    Total = v.Total,
                    UtilidadTotal = v.Detalles.Sum(d => d.Utilidad),
                    Detalles = v.Detalles.Select(d => new
                    {
                        Nombre = d.Producto != null ? d.Producto.Nombre :
                                 d.Kit != null ? "(KIT) " + d.Kit.Nombre : "N/A",
                        Cantidad = d.Cantidad,
                        Precio = d.PrecioUnitario,
                        Subtotal = d.Subtotal,
                        Utilidad = d.Utilidad
                    }).ToList()
                })
                .ToListAsync();

            decimal subtotalGeneral = ventas.Sum(v => v.Subtotal);
            decimal ivaGeneral = ventas.Sum(v => v.Iva);
            decimal totalGeneral = ventas.Sum(v => v.Total);
            decimal utilidadGeneral = ventas.Sum(v => v.UtilidadTotal);

            // Generar PDF simple con detalles
            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4.Landscape());
                    page.PageColor(Colors.White);

                    // Header
                    page.Header().Column(header =>
                    {
                        header.Item().Text("SMARTCELL COMPANY - REPORTE DE VENTAS")
                            .FontSize(14).Bold().FontColor(Colors.Blue.Darken3).AlignCenter();
                        header.Item().Text($"{desde?.ToString("dd/MM/yyyy") ?? "Inicio"} - {hasta?.ToString("dd/MM/yyyy") ?? "Fin"}")
                            .FontSize(9).AlignCenter();
                        header.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    });

                    // Contenido
                    page.Content().PaddingVertical(5).Column(col =>
                    {
                        foreach (var v in ventas)
                        {
                            col.Item().BorderBottom(1).BorderColor(Colors.Grey.Lighten2).PaddingBottom(4).Column(venta =>
                            {
                                venta.Item().Text($"Factura: {v.NoFactura}  |  Fecha: {v.Fecha:dd/MM/yyyy}  |  Cliente: {v.Cliente}  |  Vendedor: {v.Vendedor}")
                                    .FontSize(9).Bold();
                                venta.Item().Text($"NIT: {v.Nit}   |   Forma de Pago: {v.FormaPago}")
                                    .FontSize(9).FontColor(Colors.Grey.Darken2);

                                // Detalles
                                venta.Item().PaddingTop(4).Table(table =>
                                {
                                    table.ColumnsDefinition(cols =>
                                    {
                                        cols.RelativeColumn(3); // Producto
                                        cols.ConstantColumn(50); // Cantidad
                                        cols.ConstantColumn(70); // Precio
                                        cols.ConstantColumn(70); // Subtotal
                                        cols.ConstantColumn(70); // Utilidad
                                    });

                                    table.Header(header =>
                                    {
                                        header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Producto").Bold().FontSize(8);
                                        header.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignCenter().Text("Cant").Bold().FontSize(8);
                                        header.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text("Precio").Bold().FontSize(8);
                                        header.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text("Subtotal").Bold().FontSize(8);
                                        header.Cell().Background(Colors.Grey.Lighten2).Padding(3).AlignRight().Text("Utilidad").Bold().FontSize(8);
                                    });

                                    foreach (var d in v.Detalles)
                                    {
                                        table.Cell().Padding(2).Text(d.Nombre).FontSize(8);
                                        table.Cell().Padding(2).AlignCenter().Text($"{d.Cantidad}").FontSize(8);
                                        table.Cell().Padding(2).AlignRight().Text($"Q {d.Precio:N2}").FontSize(8);
                                        table.Cell().Padding(2).AlignRight().Text($"Q {d.Subtotal:N2}").FontSize(8);
                                        table.Cell().Padding(2).AlignRight().Text($"Q {d.Utilidad:N2}").FontSize(8);
                                    }
                                });

                                // Totales por venta
                                venta.Item().PaddingTop(3).AlignRight().Text(
                                    $"Subtotal: Q{v.Subtotal:N2}   IVA: Q{v.Iva:N2}   Total: Q{v.Total:N2}   Utilidad: Q{v.UtilidadTotal:N2}"
                                ).FontSize(9).Bold().FontColor(Colors.Blue.Darken2);
                            });
                        }

                        // Línea de separación
                        col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                        // Totales generales
                        col.Item().AlignRight().Text(
                            $"TOTAL GENERAL — Subtotal: Q{subtotalGeneral:N2} | IVA: Q{ivaGeneral:N2} | Total: Q{totalGeneral:N2} | Utilidad: Q{utilidadGeneral:N2}"
                        ).FontSize(10).Bold().FontColor(Colors.Green.Darken3);
                    });

                    // Footer
                    page.Footer().AlignCenter().Text(
                        $"Generado el {FechaLocal.Ahora():dd/MM/yyyy HH:mm} — Smartcell Company"
                    ).FontSize(8).FontColor(Colors.Grey.Darken2);
                });
            }).GeneratePdf();

            return File(pdfBytes, "application/pdf", $"ReporteVentas_{FechaLocal.Ahora():ddMMyyyy HHmm}.pdf");
        }






        // 🔹 Filtro de compras por proveedor
        // 🔹 Filtro de compras por proveedor (y últimas 10 por defecto)
        [HttpGet]
        public async Task<IActionResult> Compras(DateTime? desde, DateTime? hasta, int? proveedorId)
        {
            // Base query con joins mínimos necesarios
            var query = _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.UnidadMedida)
                .AsQueryable();

            if (desde.HasValue)
            {
                var desdeUtc = FechaLocal.ConvertirAUtc(desde.Value);
                query = query.Where(c => c.FechaCompra >= desdeUtc);
            }
            if (hasta.HasValue)
            {
                var hastaUtc = FechaLocal.ConvertirAUtc(hasta.Value.AddDays(1));
                query = query.Where(c => c.FechaCompra < hastaUtc);
            }
            if (proveedorId.HasValue)
                query = query.Where(c => c.IdProveedor == proveedorId);

            // Trae las compras
            var compras = await query
                .OrderByDescending(c => c.FechaCompra)
                .ToListAsync();

            // Mapa de precios vigentes por producto (Activo = true)
            // ProductoPrecio tiene: PrecioVentaSinIVA, PrecioVenta (con IVA) y por presentación (Unidad/Paquete/Caja). 
            var preciosActivos = await _context.ProductoPrecio
                .Where(p => p.Activo)
                .GroupBy(p => p.IdProducto)
                .Select(g => g.OrderByDescending(x => x.FechaInicio).First()) // por si hubiera más de uno activo, tomamos el más nuevo
                .ToDictionaryAsync(p => p.IdProducto, p => p);

            // 🔸 Proyección con factor de conversión y totales reales
            var lista = compras.Select(c =>
            {
                var dets = c.Detalles.Select(d =>
                {
                    // Factor de conversión a unidad base (ej. Paquete=6, Caja=12). Si no existe, 1.
                    var factor = d.UnidadMedida?.EquivalenciaEnUnidades ?? 1m;

                    // ✅ Precio de COMPRA por unidad individual
                    // Si guardaste el precio por presentación, dividimos entre el factor para obtener el individual.
                    var precioCompraIndividual = factor > 0 ? (d.PrecioUnitario / factor) : d.PrecioUnitario;

                    // ✅ Cantidad total en unidades base (ej. 1 paquete x 6 = 6 unidades)
                    var cantidadUnidadesBase = d.Cantidad * factor;

                    // ✅ Subtotal de compra real (precio individual * unidades base)
                    var subtotalCompra = precioCompraIndividual * cantidadUnidadesBase;

                    // Traer precios de venta vigentes (si existen)
                    preciosActivos.TryGetValue(d.IdProducto, out var precioV);

                    return new
                    {
                        Producto = d.Producto?.Nombre ?? "N/A",
                        Cantidad = d.Cantidad,
                        Unidad = d.UnidadMedida?.Nombre ?? "",
                        // Mostrar ambos precios: el guardado y el individual "real"
                        PrecioCompraUnit = d.PrecioUnitario,              // precio de la presentación elegida (como lo guardaste)
                        PrecioCompraIndividual = precioCompraIndividual,  // precio individual calculado (lo que quieres ver)
                        CantidadUnidadesBase = cantidadUnidadesBase,      // útil para debug/validación visual
                                                                          // Precios de venta vigentes
                        PrecioVentaSinIVA = precioV?.PrecioVentaSinIVA ?? 0m,
                        PrecioVentaConIVA = precioV?.PrecioVenta ?? 0m,
                        PrecioVentaUnidad = precioV?.PrecioVentaUnidad ?? 0m,
                        PrecioVentaPaquete = precioV?.PrecioVentaPaquete ?? 0m,
                        PrecioVentaCaja = precioV?.PrecioVentaCaja ?? 0m,
                        // Subtotal calculado “real”:
                        Subtotal = subtotalCompra
                    };
                }).ToList();

                // 🔸 Total de la compra basado en la suma de subtotales “reales”
                var totalCompraCalc = dets.Sum(x => x.Subtotal);

                return new
                {
                    Fecha = FechaLocal.ConvertirDeUtc(c.FechaCompra),
                    Numero = c.NumeroDocumento,
                    Proveedor = c.Proveedor.Nombre,
                    TotalCompra = totalCompraCalc,
                    Detalles = dets
                };
            }).ToList();

            // 🔸 Total general del reporte basado en los totales “recalculados”
            ViewBag.TotalGeneral = lista.Sum(c => (decimal)c.TotalCompra);

            return View("ReporteCompras", lista);

        }




        // 🔹 Descargar PDF de compras
        [HttpGet]
        public async Task<IActionResult> DescargarComprasPDF(DateTime? desde, DateTime? hasta, int? proveedorId)
        {
            var query = _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles).ThenInclude(d => d.Producto)
                .Include(c => c.Detalles).ThenInclude(d => d.UnidadMedida)
                .AsQueryable();

            if (desde.HasValue)
                query = query.Where(c => c.FechaCompra >= FechaLocal.ConvertirAUtc(desde.Value));
            if (hasta.HasValue)
                query = query.Where(c => c.FechaCompra < FechaLocal.ConvertirAUtc(hasta.Value.AddDays(1)));
            if (proveedorId.HasValue)
                query = query.Where(c => c.IdProveedor == proveedorId);

            var compras = await query
                .OrderByDescending(c => c.FechaCompra)
                .ToListAsync();

            var preciosActivos = await _context.ProductoPrecio
                .Where(p => p.Activo)
                .GroupBy(p => p.IdProducto)
                .Select(g => g.OrderByDescending(x => x.FechaInicio).First())
                .ToDictionaryAsync(p => p.IdProducto, p => p);

            var data = compras.Select(c =>
            {
                var dets = c.Detalles.Select(d =>
                {
                    var factor = d.UnidadMedida?.EquivalenciaEnUnidades ?? 1m;
                    var precioCompraIndividual = factor > 0 ? (d.PrecioUnitario / factor) : d.PrecioUnitario;
                    var cantidadUnidadesBase = d.Cantidad * factor;
                    var subtotalCompra = precioCompraIndividual * cantidadUnidadesBase;

                    preciosActivos.TryGetValue(d.IdProducto, out var precioV);

                    return new
                    {
                        Producto = d.Producto?.Nombre ?? "N/A",
                        Cantidad = d.Cantidad,
                        Unidad = d.UnidadMedida?.Nombre ?? "",
                        PrecioCompraUnit = d.PrecioUnitario,
                        PrecioCompraIndividual = precioCompraIndividual,
                        CantidadUnidadesBase = cantidadUnidadesBase,
                        PrecioVentaSinIVA = precioV?.PrecioVentaSinIVA ?? 0m,
                        PrecioVentaConIVA = precioV?.PrecioVenta ?? 0m,
                        PVUnidad = precioV?.PrecioVentaUnidad ?? 0m,
                        PVPaq = precioV?.PrecioVentaPaquete ?? 0m,
                        PVCaja = precioV?.PrecioVentaCaja ?? 0m,
                        Subtotal = subtotalCompra
                    };
                }).ToList();

                var totalCompraCalc = dets.Sum(x => x.Subtotal);

                return new
                {
                    Fecha = FechaLocal.ConvertirDeUtc(c.FechaCompra),
                    Numero = c.NumeroDocumento,
                    Proveedor = c.Proveedor.Nombre,
                    TotalCompra = totalCompraCalc,
                    Detalles = dets
                };
            }).ToList();

            var totalGeneral = data.Sum(x => x.TotalCompra);

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.PageColor(Colors.White);
                    page.Size(PageSizes.A4.Landscape());

                    page.Header().Column(h =>
                    {
                        h.Item().Text("SMARTCELL COMPANY - REPORTE DE COMPRAS")
                            .FontSize(14).Bold().FontColor(Colors.Blue.Darken3).AlignCenter();
                        h.Item().Text($"{(desde?.ToString("dd/MM/yyyy") ?? "Inicio")} - {(hasta?.ToString("dd/MM/yyyy") ?? "Fin")}")
                            .FontSize(9).AlignCenter();
                        h.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                    });

                    page.Content().Column(col =>
                    {
                        foreach (var c in data)
                        {
                            col.Item().Text($"Compra: {c.Numero}  |  Proveedor: {c.Proveedor}  |  Fecha: {c.Fecha:dd/MM/yyyy}")
                                .Bold().FontSize(10);
                            col.Item().Table(t =>
                            {
                                // Encabezados
                                t.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(3); // Producto
                                    cols.RelativeColumn(1); // Cant
                                    cols.RelativeColumn(1); // Unidad
                                    cols.RelativeColumn(1.2f); // Precio Compra
                                    cols.RelativeColumn(1.2f); // PV sin IVA
                                    cols.RelativeColumn(1.2f); // PV con IVA
                                    cols.RelativeColumn(1.2f); // PV Unidad
                                    cols.RelativeColumn(1.2f); // PV Paquete
                                    cols.RelativeColumn(1.2f); // PV Caja
                                    cols.RelativeColumn(1.2f); // Subtotal
                                });

                                t.Header(h =>
                                {
                                    h.Cell().Element(CellHeader).Text("Producto");
                                    h.Cell().Element(CellHeader).Text("Cant.");
                                    h.Cell().Element(CellHeader).Text("Unidad");
                                    h.Cell().Element(CellHeader).Text("Precio Compra");
                                    h.Cell().Element(CellHeader).Text("PV sin IVA");
                                    h.Cell().Element(CellHeader).Text("PV con IVA");
                                    h.Cell().Element(CellHeader).Text("PV Unidad");
                                    h.Cell().Element(CellHeader).Text("PV Paquete");
                                    h.Cell().Element(CellHeader).Text("PV Caja");
                                    h.Cell().Element(CellHeader).Text("Subtotal");
                                });

                                foreach (var d in c.Detalles)
                                {
                                    t.Cell().Element(CellBody).Text(d.Producto);
                                    t.Cell().Element(CellBody).Text($"{d.Cantidad:N2}");
                                    t.Cell().Element(CellBody).Text(d.Unidad);
                                    t.Cell().Element(CellBody).Text($"Q{d.PrecioCompraUnit:N2}");
                                    t.Cell().Element(CellBody).Text($"Q{d.PrecioVentaSinIVA:N2}");
                                    t.Cell().Element(CellBody).Text($"Q{d.PrecioVentaConIVA:N2}");
                                    t.Cell().Element(CellBody).Text($"Q{d.PVUnidad:N2}");
                                    t.Cell().Element(CellBody).Text($"Q{d.PVPaq:N2}");
                                    t.Cell().Element(CellBody).Text($"Q{d.PVCaja:N2}");
                                    t.Cell().Element(CellBody).Text($"Q{d.Subtotal:N2}");
                                }
                            });

                            // Totales por compra
                            col.Item().PaddingTop(3).AlignRight()
                                .Text($"Total compra: Q{c.TotalCompra:N2}")
                                .FontSize(9).Bold().FontColor(Colors.Blue.Darken2);

                            col.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        }

                        // Total general
                        col.Item().AlignRight()
                            .Text($"TOTAL GENERAL: Q{totalGeneral:N2}")
                            .FontSize(10).Bold().FontColor(Colors.Green.Darken3);
                    });

                    page.Footer().AlignCenter().Text(
                        $"Generado el {FechaLocal.Ahora():dd/MM/yyyy HH:mm} — Smartcell Company"
                    ).FontSize(8).FontColor(Colors.Grey.Darken2);
                });

                static IContainer CellHeader(IContainer c) => c
                    .PaddingVertical(2).PaddingHorizontal(4)
                    .Background(Colors.Grey.Lighten3)
                    .BorderBottom(1).BorderColor(Colors.Grey.Medium)
                    .DefaultTextStyle(x => x.SemiBold().FontSize(9));

                static IContainer CellBody(IContainer c) => c
                    .PaddingVertical(2).PaddingHorizontal(4)
                    .DefaultTextStyle(x => x.FontSize(9));
            }).GeneratePdf();

            return File(pdfBytes, "application/pdf", $"ReporteCompras_{FechaLocal.Ahora():yyyyMMdd_HHmm}.pdf");
        }





        // reporte Inventario
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> ReporteInventario(int? idCategoria, int? idProveedor, string nombre, bool pdf = false)
        {
            // --- Filtros y datos base ---
            ViewBag.Categorias = await _context.Categorias.OrderBy(c => c.Nombre).ToListAsync();
            ViewBag.Proveedores = await _context.Proveedores.Where(p => p.Activo).OrderBy(p => p.Nombre).ToListAsync();
            ViewBag.IdCategoria = idCategoria;
            ViewBag.IdProveedor = idProveedor;
            ViewBag.Nombre = nombre ?? "";

            var productosQuery = _context.Productos.AsQueryable();

            if (idCategoria.HasValue)
                productosQuery = productosQuery.Where(p => p.IdCategoria == idCategoria.Value);
            if (!string.IsNullOrWhiteSpace(nombre))
                productosQuery = productosQuery.Where(p => p.Nombre!.Contains(nombre));
            if (idProveedor.HasValue)
            {
                var idsProductosProveedor = await _context.ProductosProveedores
                    .Where(pp => pp.IdProveedor == idProveedor.Value)
                    .Select(pp => pp.IdProducto)
                    .Distinct()
                    .ToListAsync();

                productosQuery = productosQuery.Where(p => idsProductosProveedor.Contains(p.IdProducto));
            }

            var preciosActivos = await _context.ProductoPrecio
                .Where(pp => pp.Activo)
                .GroupBy(pp => pp.IdProducto)
                .Select(g => g.OrderByDescending(x => x.FechaInicio).FirstOrDefault())
                .ToListAsync();

            var productosList = await productosQuery
                .Include(p => p.Categoria)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            var data = new List<object>();

            foreach (var p in productosList)
            {
                var inventario = await _context.Inventarios.FirstOrDefaultAsync(i => i.IdProducto == p.IdProducto);
                var precio = preciosActivos.FirstOrDefault(x => x.IdProducto == p.IdProducto);

                var prodProv = await _context.ProductosProveedores
                    .Where(pp => pp.IdProducto == p.IdProducto)
                    .Join(_context.Proveedores,
                          pp => pp.IdProveedor,
                          pr => pr.IdProveedor,
                          (pp, pr) => new { pr.Nombre })
                    .FirstOrDefaultAsync();

                var proveedorNombre = prodProv?.Nombre ?? "";

                decimal precioSinIVA = precio?.PrecioBase ?? 0m;
                decimal precioConIVA = precio?.PrecioVenta ?? 0m;
                int stockActual = inventario?.StockActual ?? 0;
                int stockMinimo = inventario?.StockMinimo ?? 0;
                decimal valorTotal = stockActual * precioConIVA;

                data.Add(new
                {
                    p.IdProducto,
                    p.Nombre,
                    Categoria = p.Categoria?.Nombre ?? "",
                    Proveedor = proveedorNombre,
                    StockActual = stockActual,
                    StockMinimo = stockMinimo,
                    PrecioSinIVA = precioSinIVA,
                    PrecioConIVA = precioConIVA,
                    ValorTotal = valorTotal
                });
            }

            // --- PDF con nuevo estilo ---
            if (pdf)
            {
                var totalInventario = data.Cast<dynamic>().Sum(d => (decimal)d.ValorTotal);

                var pdfBytes = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(25);
                        page.Size(PageSizes.A4.Landscape());
                        page.PageColor(Colors.White);

                        // HEADER
                        page.Header().Column(header =>
                        {
                            header.Item().Text("SMARTCELL COMPANY - REPORTE DE INVENTARIO")
                                .FontSize(14).Bold().FontColor(Colors.Blue.Darken3).AlignCenter();
                            header.Item().Text($"Fecha: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(9).AlignCenter().FontColor(Colors.Grey.Darken2);
                            header.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        });

                        // CONTENIDO (solo una llamada)
                        page.Content().PaddingVertical(5).Column(col =>
                        {
                            // Tabla
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.RelativeColumn(2);   // Producto
                                    cols.RelativeColumn(1.5f); // Categoría
                                    cols.RelativeColumn(1.5f); // Proveedor
                                    cols.ConstantColumn(55);    // Stock
                                    cols.ConstantColumn(55);    // Mínimo
                                    cols.ConstantColumn(75);    // Precio sin IVA
                                    cols.ConstantColumn(75);    // Precio con IVA
                                    cols.ConstantColumn(90);    // Total
                                });

                                // Encabezado
                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Producto").Bold().FontColor(Colors.White).FontSize(8);
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Categoría").Bold().FontColor(Colors.White).FontSize(8);
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Proveedor").Bold().FontColor(Colors.White).FontSize(8);
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).AlignCenter().Text("Stock").Bold().FontColor(Colors.White).FontSize(8);
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).AlignCenter().Text("Min").Bold().FontColor(Colors.White).FontSize(8);
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).AlignRight().Text("Sin IVA").Bold().FontColor(Colors.White).FontSize(8);
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).AlignRight().Text("Con IVA").Bold().FontColor(Colors.White).FontSize(8);
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).AlignRight().Text("Total").Bold().FontColor(Colors.White).FontSize(8);
                                });

                                int i = 0;
                                foreach (var item in data)
                                {
                                    var nombre = (item.GetType().GetProperty("Nombre")?.GetValue(item) ?? "").ToString() ?? "";
                                    var categoria = (item.GetType().GetProperty("Categoria")?.GetValue(item) ?? "").ToString() ?? "";
                                    var proveedor = (item.GetType().GetProperty("Proveedor")?.GetValue(item) ?? "").ToString() ?? "";
                                    int stockActual = Convert.ToInt32(item.GetType().GetProperty("StockActual")?.GetValue(item) ?? 0);
                                    int stockMinimo = Convert.ToInt32(item.GetType().GetProperty("StockMinimo")?.GetValue(item) ?? 0);
                                    decimal precioSinIVA = Convert.ToDecimal(item.GetType().GetProperty("PrecioSinIVA")?.GetValue(item) ?? 0m);
                                    decimal precioConIVA = Convert.ToDecimal(item.GetType().GetProperty("PrecioConIVA")?.GetValue(item) ?? 0m);
                                    decimal valorTotal = Convert.ToDecimal(item.GetType().GetProperty("ValorTotal")?.GetValue(item) ?? 0m);

                                    var bg = i++ % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                                    table.Cell().Background(bg).Padding(3).Text(nombre).FontSize(8);
                                    table.Cell().Background(bg).Padding(3).Text(categoria).FontSize(8);
                                    table.Cell().Background(bg).Padding(3).Text(proveedor).FontSize(8);
                                    table.Cell().Background(bg).Padding(3).AlignCenter().Text(stockActual.ToString()).FontSize(8);
                                    table.Cell().Background(bg).Padding(3).AlignCenter().Text(stockMinimo.ToString()).FontSize(8);
                                    table.Cell().Background(bg).Padding(3).AlignRight().Text($"Q {precioSinIVA:N2}").FontSize(8);
                                    table.Cell().Background(bg).Padding(3).AlignRight().Text($"Q {precioConIVA:N2}").FontSize(8);
                                    table.Cell().Background(bg).Padding(3).AlignRight().Text($"Q {valorTotal:N2}").FontSize(8);
                                }
                            });

                            // Resumen (en la misma columna)
                            col.Item().PaddingTop(10).AlignRight().Text(
                                $"VALOR TOTAL DEL INVENTARIO: Q {totalInventario:N2}"
                            ).FontSize(10).Bold().FontColor(Colors.Green.Darken3);
                        });

                        // FOOTER
                        page.Footer().AlignCenter().Text(
                            "Smartcell Company — Sistema de Gestión Comercial"
                        ).FontSize(8).FontColor(Colors.Grey.Darken2);
                    });
                }).GeneratePdf();

                return File(pdfBytes, "application/pdf", "ReporteInventario.pdf");
            }

            // Vista normal
            ViewBag.TotalInventario = data.Cast<dynamic>().Sum(d => (decimal)d.ValorTotal);
            return View("~/Views/Reportes/ReporteInventario.cshtml", data);
        }





        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> ReporteAjustesInventario(DateTime? desde, DateTime? hasta, string nombreUsuario, string tipo, bool pdf = false)
        {
            var query = _context.MovInventarios
                .Include(m => m.Producto)
                .Include(m => m.Producto.Categoria)
                .Include(m => m.Producto.Marca)
                .Where(m => m.TipoMovimiento == "Ajuste Manual")
                .AsQueryable();

            // 🔹 Filtros
            if (desde.HasValue)
                query = query.Where(m => m.Fecha >= desde.Value);

            if (hasta.HasValue)
                query = query.Where(m => m.Fecha <= hasta.Value.AddDays(1));

            if (!string.IsNullOrEmpty(nombreUsuario))
                query = query.Where(m => m.Referencia.Contains(nombreUsuario));

            if (!string.IsNullOrEmpty(tipo))
            {
                if (tipo == "entrada")
                    query = query.Where(m => m.Cantidad > 0);
                else if (tipo == "salida")
                    query = query.Where(m => m.Cantidad < 0);
            }

            var data = await query
                .OrderByDescending(m => m.Fecha)
                .Select(m => new
                {
                    Fecha = m.Fecha,
                    Producto = m.Producto.Nombre,
                    Cantidad = m.Cantidad,
                    Tipo = m.Cantidad > 0 ? "Entrada" : "Salida",
                    Motivo = m.Referencia
                })
                .ToListAsync();

            // 🔹 Generar PDF con estilo
            if (pdf)
            {
                var pdfBytes = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(25);
                        page.Size(PageSizes.A4);
                        page.PageColor(Colors.White);

                        // 🔸 Encabezado
                        page.Header().Column(header =>
                        {
                            header.Item().Text("SMARTCELL COMPANY - REPORTE DE AJUSTES DE INVENTARIO")
                                .FontSize(14).Bold().FontColor(Colors.Blue.Darken3).AlignCenter();

                            header.Item().Text($"Generado: {DateTime.Now:dd/MM/yyyy HH:mm}")
                                .FontSize(9).AlignCenter().FontColor(Colors.Grey.Darken2);

                            header.Item().LineHorizontal(1).LineColor(Colors.Grey.Lighten1);
                        });

                        // 🔸 Contenido principal
                        page.Content().PaddingVertical(5).Column(col =>
                        {
                            col.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(90);   // Fecha
                                    cols.RelativeColumn(2);    // Producto
                                    cols.ConstantColumn(70);   // Cantidad
                                    cols.ConstantColumn(70);   // Tipo
                                    cols.RelativeColumn(3);    // Motivo
                                });

                                // Encabezado de tabla
                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Fecha").Bold().FontColor(Colors.White).FontSize(9);
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Producto").Bold().FontColor(Colors.White).FontSize(9);
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).AlignCenter().Text("Cantidad").Bold().FontColor(Colors.White).FontSize(9);
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).AlignCenter().Text("Tipo").Bold().FontColor(Colors.White).FontSize(9);
                                    header.Cell().Background(Colors.Blue.Darken3).Padding(4).Text("Motivo").Bold().FontColor(Colors.White).FontSize(9);
                                });

                                int i = 0;
                                foreach (var item in data)
                                {
                                    var bg = i++ % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;
                                    table.Cell().Background(bg).Padding(3).Text(item.Fecha.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                                    table.Cell().Background(bg).Padding(3).Text(item.Producto).FontSize(9);
                                    table.Cell().Background(bg).Padding(3).AlignCenter().Text(item.Cantidad.ToString()).FontSize(9);
                                    table.Cell().Background(bg).Padding(3).AlignCenter()
                                        .Text(item.Tipo)
                                        .FontColor(item.Tipo == "Entrada" ? Colors.Green.Darken2 : Colors.Red.Darken2)
                                        .Bold().FontSize(9);
                                    table.Cell().Background(bg).Padding(3).Text(item.Motivo ?? "-").FontSize(9);
                                }
                            });

                            // Espacio
                            col.Item().PaddingTop(10);

                            // Resumen
                            col.Item().AlignRight().Text($"Total de ajustes: {data.Count}")
                                .FontSize(10).Bold().FontColor(Colors.Green.Darken3);
                        });

                        // 🔸 Pie de página
                        page.Footer().AlignCenter().Text("Smartcell Company — Sistema de Gestión Comercial")
                            .FontSize(8).FontColor(Colors.Grey.Darken2);
                    });
                }).GeneratePdf();

                return File(pdfBytes, "application/pdf", $"ReporteAjustesInventario_{FechaLocal.Ahora():ddMMyyyy_HHmm}.pdf");
            }

            return View("~/Views/Reportes/ReporteAjustesInventario.cshtml", data);
        }





        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> Actividades(DateTime? desde, DateTime? hasta, int? idUsuario)
        {
            var query = _context.BitacoraMovimientos
                .Include(b => b.Usuario)
                .AsQueryable();

            if (desde.HasValue)
                query = query.Where(b => b.Fecha >= desde.Value);
            if (hasta.HasValue)
                query = query.Where(b => b.Fecha <= hasta.Value);
            if (idUsuario.HasValue)
                query = query.Where(b => b.IdUsuario == idUsuario.Value);

            var movimientos = await query
                .OrderByDescending(b => b.Fecha)
                .ToListAsync();

            ViewBag.Usuarios = await _context.Usuarios
                .OrderBy(u => u.NombreUsuario)
                .ToListAsync();

            return View("~/Views/Reportes/Actividades.cshtml", movimientos);
        }




    }
}