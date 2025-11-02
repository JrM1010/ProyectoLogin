using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Recursos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;

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
        Fecha = FechaLocal.ConvertirDeUtc(v.FechaVenta),
        NoFactura = v.NumeroFactura,
        // 🔹 Si no tiene nombre ni apellido, mostrar "CF"
        Cliente = (string.IsNullOrWhiteSpace(v.Cliente.Nombres) && string.IsNullOrWhiteSpace(v.Cliente.Apellidos))
                    ? "CF"
                    : (v.Cliente.Nombres + " " + v.Cliente.Apellidos),
        // 🔹 Si el NIT está vacío, mostrar "CF"
        Nit = string.IsNullOrWhiteSpace(v.Cliente.Nit) ? "CF" : v.Cliente.Nit,
        Vendedor = v.Usuario.NombreUsuario,
        TotalSinIva = v.Total / 1.12m,
        ValorIva = v.Total - (v.Total / 1.12m),
        TotalConIva = v.Total,
        FormaPago = v.MetodoPago
    })
    .ToListAsync();

            // Calcular totales
            ViewBag.SubtotalGeneral = ventas.Sum(v => v.TotalSinIva);
            ViewBag.IvaGeneral = ventas.Sum(v => v.ValorIva);
            ViewBag.TotalGeneral = ventas.Sum(v => v.TotalConIva);

            return View("ReporteVentas", ventas);
        }

        // 🔹 Generar PDF del reporte de ventas
        public async Task<IActionResult> DescargarPDF(DateTime? desde, DateTime? hasta, string vendedor, string formaPago)
        {
            var query = _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.Usuario)
                .AsQueryable();

            // 🔹 APLICAR MISMA CONVERSIÓN DE FECHAS QUE EN EL MÉTODO Ventas
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

            var ventas = await query
                .OrderBy(v => v.FechaVenta)
                .ToListAsync();

            var subtotal = ventas.Sum(v => v.Total / 1.12m);
            var iva = ventas.Sum(v => v.Total - (v.Total / 1.12m));
            var total = ventas.Sum(v => v.Total);

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);

                    // 🔹 ENCABEZADO
                    page.Header().Element(ComposeHeader);

                    // 🔹 CONTENIDO PRINCIPAL
                    page.Content().PaddingVertical(10).Element(content =>
                    {
                        content.Column(column =>
                        {
                            // ---- TABLA DE VENTAS ----
                            column.Item().Table(table =>
                            {
                                // 🔹 COLUMNAS AJUSTADAS AL ESPACIO DISPONIBLE
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(50);   // Fecha (reducida)
                                    columns.ConstantColumn(65);   // Factura  
                                    columns.RelativeColumn(1.5f); // Cliente (ajustada)
                                    columns.ConstantColumn(70);   // NIT (reducida)
                                    columns.RelativeColumn(1);    // Vendedor
                                    columns.ConstantColumn(65);   // Subtotal (reducida)
                                    columns.ConstantColumn(50);   // IVA (reducida)
                                    columns.ConstantColumn(65);   // Total (reducida)
                                    columns.ConstantColumn(75);   // Forma de pago
                                });

                                // 🔹 Encabezado tabla con fuentes más pequeñas
                                table.Header(header =>
                                {
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Fecha").Bold().FontSize(8);
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Factura").Bold().FontSize(8);
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Cliente").Bold().FontSize(8);
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("NIT").Bold().FontSize(8);
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Vendedor").Bold().FontSize(8);
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Subtotal").Bold().FontSize(8);
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("IVA").Bold().FontSize(8);
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Total").Bold().FontSize(8);
                                    header.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Forma Pago").Bold().FontSize(8);
                                });

                                // 🔹 Filas de datos con fuentes más pequeñas
                                foreach (var v in ventas)
                                {
                                    var sub = v.Total / 1.12m;
                                    var imp = v.Total - sub;
                                    var fechaLocal = FechaLocal.ConvertirDeUtc(v.FechaVenta);

                                    // 🔹 Si el cliente o NIT están vacíos, mostrar "CF"
                                    // 🔹 Verificar si el cliente no tiene nombre o NIT, mostrar "CF"
                                    var nombreCliente = (string.IsNullOrWhiteSpace(v.Cliente.Nombres) && string.IsNullOrWhiteSpace(v.Cliente.Apellidos))
                                        ? "CF"
                                        : $"{v.Cliente.Nombres} {v.Cliente.Apellidos}";
                                    var nitCliente = string.IsNullOrWhiteSpace(v.Cliente.Nit) ? "CF" : v.Cliente.Nit;

                                    table.Cell().BorderBottom(0.5f).Padding(1).Text(fechaLocal.ToString("dd/MM/yyyy")).FontSize(8);
                                    table.Cell().BorderBottom(0.5f).Padding(1).Text(v.NumeroFactura).FontSize(8);
                                    table.Cell().BorderBottom(0.5f).Padding(1).Text(TruncateText(nombreCliente, 20)).FontSize(8);
                                    table.Cell().BorderBottom(0.5f).Padding(1).Text(TruncateText(nitCliente, 12)).FontSize(8);

                                    table.Cell().BorderBottom(0.5f).Padding(1).Text(TruncateText(v.Usuario.NombreUsuario, 15)).FontSize(8);
                                    table.Cell().BorderBottom(0.5f).Padding(1).AlignRight().Text($"Q{sub:F2}").FontSize(8);
                                    table.Cell().BorderBottom(0.5f).Padding(1).AlignRight().Text($"Q{imp:F2}").FontSize(8);
                                    table.Cell().BorderBottom(0.5f).Padding(1).AlignRight().Text($"Q{v.Total:F2}").FontSize(8);
                                    table.Cell().BorderBottom(0.5f).Padding(1).Text(TruncateText(v.MetodoPago, 10)).FontSize(8);
                                }
                            });

                            // ---- ESPACIADO ----
                            column.Item().PaddingVertical(10);

                            // ---- TOTALES GENERALES ----
                            column.Item().Table(t =>
                            {
                                t.ColumnsDefinition(c =>
                                {
                                    c.RelativeColumn(6);
                                    c.ConstantColumn(100);
                                });

                                t.Cell().AlignRight().Text("Subtotal general:").Bold().FontSize(10);
                                t.Cell().AlignRight().Text($"Q{subtotal:F2}").FontSize(10);

                                t.Cell().AlignRight().Text("IVA general:").Bold().FontSize(10);
                                t.Cell().AlignRight().Text($"Q{iva:F2}").FontSize(10);

                                t.Cell().AlignRight().Text("Total general:").Bold().FontSize(11).FontColor(Colors.Green.Darken2);
                                t.Cell().AlignRight().Text($"Q{total:F2}").Bold().FontSize(11).FontColor(Colors.Green.Darken2);
                            });
                        });
                    });

                    // 🔹 PIE DE PÁGINA
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Generado el ").FontSize(9);
                        text.Span($"{FechaLocal.Ahora():dd/MM/yyyy HH:mm}").FontSize(9); // 🔹 FORMATO CONSISTENTE
                    });
                });

                // 🔹 MÉTODO PARA EL ENCABEZADO
                void ComposeHeader(IContainer header)
                {
                    header.Row(row =>
                    {
                        row.RelativeColumn().Column(col =>
                        {
                            col.Item().Text("SMARTCELL COMPANY")
                                .FontSize(14).Bold().FontColor(Colors.Blue.Medium);
                            col.Item().Text("Reporte de Ventas").FontSize(11);
                            col.Item().Text($"Generado: {FechaLocal.Ahora():dd/MM/yyyy HH:mm}") // 🔹 FORMATO CONSISTENTE
                                .FontSize(9).FontColor(Colors.Grey.Darken1);
                        });
                    });
                }
            });

            var pdf = doc.GeneratePdf();
            return File(pdf, "application/pdf", $"ReporteVentas_{FechaLocal.Ahora():dd-MM-yyyy-HHmm}.pdf");
        }

        // 🔹 MÉTODO AUXILIAR PARA TRUNCAR TEXTO LARGO
        private string TruncateText(string text, int maxLength)
        {
            if (string.IsNullOrEmpty(text)) return text;
            return text.Length <= maxLength ? text : text.Substring(0, maxLength - 3) + "...";
        }




        // 🔹 Filtro de compras por proveedor
        public async Task<IActionResult> Compras(DateTime? desde, DateTime? hasta, int? proveedorId)
        {
            var query = _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
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

            var compras = await query
                .OrderByDescending(c => c.FechaCompra)
                .ToListAsync();

            var lista = compras.Select(c => new
            {
                Fecha = FechaLocal.ConvertirDeUtc(c.FechaCompra),
                Proveedor = c.Proveedor.Nombre,
                Productos = string.Join(", ", c.Detalles.Select(d => d.Producto.Nombre)),
                TotalCompra = c.Total,
                Detalles = c.Detalles.Select(d => new
                {
                    d.Producto.Nombre,
                    d.Cantidad,
                    PrecioCompra = d.PrecioUnitario
                }).ToList()
            }).ToList();

            ViewBag.Proveedores = await _context.Proveedores
                .Where(p => p.Activo)
                .OrderBy(p => p.Nombre)
                .ToListAsync();

            ViewBag.TotalGeneral = lista.Sum(c => c.TotalCompra);

            return View("ReporteCompras", lista);
        }

        // 🔹 Descargar PDF de compras
        public async Task<IActionResult> DescargarComprasPDF(DateTime? desde, DateTime? hasta, int? proveedorId)
        {
            var query = _context.Compras
                .Include(c => c.Proveedor)
                .Include(c => c.Detalles)
                    .ThenInclude(d => d.Producto)
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

            var compras = await query
                .OrderByDescending(c => c.FechaCompra)
                .ToListAsync();

            var totalGeneral = compras.Sum(c => c.Total);

            var doc = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(40);
                    page.Header().Text("Reporte de Compras por Proveedor")
                        .FontSize(14).Bold().FontColor(Colors.Blue.Medium);
                    page.Content().PaddingVertical(10).Element(content =>
                    {
                        content.Column(column =>
                        {
                            column.Item().Table(table =>
                            {
                                table.ColumnsDefinition(cols =>
                                {
                                    cols.ConstantColumn(60);
                                    cols.RelativeColumn(2);
                                    cols.RelativeColumn(3);
                                    cols.ConstantColumn(80);
                                });

                                table.Header(h =>
                                {
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Fecha").Bold().FontSize(9);
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Proveedor").Bold().FontSize(9);
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Productos Comprados").Bold().FontSize(9);
                                    h.Cell().Background(Colors.Grey.Lighten2).Padding(3).Text("Total (Q)").Bold().FontSize(9);
                                });

                                foreach (var c in compras)
                                {
                                    var fechaLocal = FechaLocal.ConvertirDeUtc(c.FechaCompra);
                                    var productos = string.Join(", ", c.Detalles.Select(d => d.Producto.Nombre));
                                    table.Cell().Padding(2).Text(fechaLocal.ToString("dd/MM/yyyy")).FontSize(9);
                                    table.Cell().Padding(2).Text(c.Proveedor.Nombre).FontSize(9);
                                    table.Cell().Padding(2).Text(TruncateText(productos, 40)).FontSize(9);
                                    table.Cell().Padding(2).AlignRight().Text($"Q{c.Total:F2}").FontSize(9);
                                }
                            });

                            column.Item().PaddingVertical(10);
                            column.Item().AlignRight().Text($"TOTAL GENERAL: Q{totalGeneral:F2}")
                                .Bold().FontSize(11).FontColor(Colors.Green.Darken2);
                        });
                    });

                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Generado el ").FontSize(9);
                        text.Span($"{FechaLocal.Ahora():dd/MM/yyyy HH:mm}").FontSize(9);
                    });
                });
            });

            var pdf = doc.GeneratePdf();
            return File(pdf, "application/pdf", $"ReporteCompras_{FechaLocal.Ahora():dd-MM-yyyy-HHmm}.pdf");
        }



    }
}