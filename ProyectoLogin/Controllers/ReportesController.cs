using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ProyectoLogin.Models;
using ProyectoLogin.Models.ModelosVentas;
using ProyectoLogin.Recursos;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QuestPDF.Drawing;
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
                    FormaPago = v.MetodoPago,
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
                        EsKit = d.IdKit.HasValue
                    }).ToList()
                })
                .ToListAsync();

            // Calcular totales
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
                .AsQueryable();

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
                    TotalSinIva = v.Total / 1.12m,
                    ValorIva = v.Total - (v.Total / 1.12m),
                    TotalConIva = v.Total,
                    FormaPago = v.MetodoPago
                })
                .ToListAsync();

            decimal subtotalGeneral = ventas.Sum(v => v.TotalSinIva);
            decimal ivaGeneral = ventas.Sum(v => v.ValorIva);
            decimal totalGeneral = ventas.Sum(v => v.TotalConIva);

            // Generar PDF mejorado
            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(30);
                    page.Size(PageSizes.A4.Landscape());
                    page.PageColor(Colors.White);

                    // Header mejorado
                    page.Header().Column(header =>
                    {
                        // Logo y título
                        header.Item().Row(row =>
                        {
                            row.RelativeItem().Column(col =>
                            {
                                col.Item().Text("Smartcell Company").FontSize(16).Bold().FontColor(Colors.Blue.Darken3);
                                col.Item().Text("Reporte de Ventas").FontSize(12).SemiBold().FontColor(Colors.Grey.Darken2);
                            });

                            row.ConstantItem(100).AlignRight().Text(txt =>
                            {
                                txt.Span("Fecha: ").SemiBold().FontSize(9);
                                txt.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm")).FontSize(9);
                            });
                        });

                        // Línea separadora
                        header.Item().PaddingTop(5).PaddingBottom(10).LineHorizontal(1).LineColor(Colors.Blue.Medium);

                        // Filtros aplicados en tarjetas
                        header.Item().PaddingBottom(15).Row(filterRow =>
                        {
                            filterRow.RelativeItem().Background(Colors.Grey.Lighten3).Padding(8).Border(1).BorderColor(Colors.Grey.Lighten1).Column(col =>
                            {
                                col.Item().Text("Período").FontSize(8).Bold().FontColor(Colors.Grey.Darken2);
                                col.Item().Text($"{desde?.ToString("dd/MM/yyyy") ?? "Inicio"} - {hasta?.ToString("dd/MM/yyyy") ?? "Fin"}").FontSize(9);
                            });

                            filterRow.RelativeItem().PaddingLeft(5).Background(Colors.Grey.Lighten3).Padding(8).Border(1).BorderColor(Colors.Grey.Lighten1).Column(col =>
                            {
                                col.Item().Text("Vendedor").FontSize(8).Bold().FontColor(Colors.Grey.Darken2);
                                col.Item().Text(!string.IsNullOrEmpty(vendedor) ? vendedor : "Todos").FontSize(9);
                            });

                            filterRow.RelativeItem().PaddingLeft(5).Background(Colors.Grey.Lighten3).Padding(8).Border(1).BorderColor(Colors.Grey.Lighten1).Column(col =>
                            {
                                col.Item().Text("Forma de Pago").FontSize(8).Bold().FontColor(Colors.Grey.Darken2);
                                col.Item().Text(!string.IsNullOrEmpty(formaPago) ? formaPago : "Todas").FontSize(9);
                            });
                        });
                    });

                    // Content mejorado
                    page.Content().PaddingVertical(5).Column(col =>
                    {
                        // Tabla de ventas con mejor diseño
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);   // Fecha
                                columns.ConstantColumn(75);   // Factura
                                columns.RelativeColumn(2);    // Cliente
                                columns.ConstantColumn(70);   // NIT
                                columns.RelativeColumn(1.2f); // Vendedor
                                columns.ConstantColumn(75);   // Subtotal
                                columns.ConstantColumn(65);   // IVA
                                columns.ConstantColumn(75);   // Total
                                columns.ConstantColumn(80);   // FormaPago
                            });

                            // Header con estilo mejorado
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).AlignCenter().Text("Fecha").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).AlignCenter().Text("Factura").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).AlignCenter().Text("Cliente").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).AlignCenter().Text("NIT").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).AlignCenter().Text("Vendedor").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).AlignCenter().Text("Subtotal").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).AlignCenter().Text("IVA").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).AlignCenter().Text("Total").Bold().FontColor(Colors.White).FontSize(9);
                                header.Cell().Background(Colors.Blue.Darken3).Padding(5).AlignCenter().Text("Forma Pago").Bold().FontColor(Colors.White).FontSize(9);
                            });

                            // Filas con estilo zebra
                            for (int i = 0; i < ventas.Count; i++)
                            {
                                var v = ventas[i];
                                var backgroundColor = i % 2 == 0 ? Colors.White : Colors.Grey.Lighten5;

                                table.Cell().Background(backgroundColor).PaddingVertical(4).PaddingHorizontal(3).Text(v.Fecha.ToString("dd/MM/yyyy")).FontSize(8);
                                table.Cell().Background(backgroundColor).PaddingVertical(4).PaddingHorizontal(3).Text(v.NoFactura ?? "").FontSize(8);
                                table.Cell().Background(backgroundColor).PaddingVertical(4).PaddingHorizontal(3).Text(v.Cliente ?? "").FontSize(8);
                                table.Cell().Background(backgroundColor).PaddingVertical(4).PaddingHorizontal(3).Text(v.Nit ?? "").FontSize(8);
                                table.Cell().Background(backgroundColor).PaddingVertical(4).PaddingHorizontal(3).Text(v.Vendedor ?? "").FontSize(8);
                                table.Cell().Background(backgroundColor).PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text($"Q {v.TotalSinIva:N2}").FontSize(8);
                                table.Cell().Background(backgroundColor).PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text($"Q {v.ValorIva:N2}").FontSize(8);
                                table.Cell().Background(backgroundColor).PaddingVertical(4).PaddingHorizontal(3).AlignRight().Text($"Q {v.TotalConIva:N2}").FontSize(8);
                                table.Cell().Background(backgroundColor).PaddingVertical(4).PaddingHorizontal(3).Text(v.FormaPago ?? "").FontSize(8);
                            }
                        });

                        // Resumen general con diseño de tarjeta
                        col.Item().PaddingTop(15).Row(row =>
                        {
                            row.ConstantItem(250).Background(Colors.Green.Lighten5).Padding(12).Border(1).BorderColor(Colors.Green.Lighten2).Column(totalCol =>
                            {
                                totalCol.Item().Text("RESUMEN GENERAL").FontSize(11).Bold().FontColor(Colors.Green.Darken3);
                                totalCol.Item().PaddingTop(5).Row(resumenRow =>
                                {
                                    resumenRow.RelativeItem().Text("Subtotal:").FontSize(10);
                                    resumenRow.ConstantItem(100).AlignRight().Text($"Q{subtotalGeneral:N2}").FontSize(10);
                                });
                                totalCol.Item().Row(resumenRow =>
                                {
                                    resumenRow.RelativeItem().Text("IVA:").FontSize(10);
                                    resumenRow.ConstantItem(100).AlignRight().Text($"Q{ivaGeneral:N2}").FontSize(10);
                                });
                                totalCol.Item().Row(resumenRow =>
                                {
                                    resumenRow.RelativeItem().Text("Total:").FontSize(11).Bold();
                                    resumenRow.ConstantItem(100).AlignRight().Text($"Q{totalGeneral:N2}").FontSize(11).Bold();
                                });
                            });

                            // Estadísticas adicionales
                            row.RelativeItem().PaddingLeft(10).Background(Colors.Blue.Lighten5).Padding(12).Border(1).BorderColor(Colors.Blue.Lighten2).Column(statsCol =>
                            {
                                statsCol.Item().Text("ESTADÍSTICAS").FontSize(11).Bold().FontColor(Colors.Blue.Darken3);
                                statsCol.Item().PaddingTop(5).Text($"Total Ventas: {ventas.Count}").FontSize(10);
                                statsCol.Item().Text($"Promedio por Venta: Q{(ventas.Count > 0 ? totalGeneral / ventas.Count : 0):N2}").FontSize(10);
                            });
                        });
                    });

                    // Footer mejorado - CORREGIDO
                    page.Footer().Background(Colors.Grey.Lighten3).Padding(8).Row(footer =>
                    {
                        footer.RelativeItem().AlignLeft().Text(txt =>
                        {
                            txt.Span("Smartcell Company - ").FontSize(8).SemiBold();
                            txt.Span("Sistema de Gestión Comercial").FontSize(8);
                        });
                        footer.RelativeItem().AlignRight().Text(txt =>
                        {
                            txt.CurrentPageNumber().FontSize(8).Bold();
                            txt.Span(" / ").FontSize(8);
                            txt.TotalPages().FontSize(8).Bold();
                        });
                    });
                });
            }).GeneratePdf();

            return File(pdfBytes, "application/pdf", $"ReporteVentas_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
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
                .Select(c => new
                {
                    Fecha = FechaLocal.ConvertirDeUtc(c.FechaCompra),
                    Proveedor = c.Proveedor.Nombre,
                    
                    Productos = string.Join(", ", c.Detalles.Select(d => d.Producto.Nombre)),
                    Total = c.Total
                })
                .ToListAsync();

            var totalGeneral = compras.Sum(c => c.Total);
            var totalCompras = compras.Count;

            var pdfBytes = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);
                    page.PageColor(Colors.White);

                    // Header simple
                    page.Header().Column(header =>
                    {
                        header.Item().AlignCenter().Text("Reporte de Compras")
                            .FontSize(16).Bold().FontColor(Colors.Blue.Darken3);

                        header.Item().PaddingVertical(5).LineHorizontal(1).LineColor(Colors.Grey.Lighten1);

                        // Información de filtros
                        header.Item().PaddingBottom(10).Column(filterCol =>
                        {
                            filterCol.Item().Text(text =>
                            {
                                text.Span("Período: ").SemiBold();
                                text.Span($"{desde?.ToString("dd/MM/yyyy") ?? "Todos"} - {hasta?.ToString("dd/MM/yyyy") ?? "Todos"}");
                            });

                            filterCol.Item().Text(text =>
                            {
                                text.Span("Total de compras: ").SemiBold();
                                text.Span($"{totalCompras}");
                            });
                        });
                    });

                    // Contenido principal
                    page.Content().PaddingVertical(10).Column(col =>
                    {
                        // Tabla simple
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(70);   // Fecha
                                columns.RelativeColumn(2);    // Proveedor
                                columns.RelativeColumn(2);    // Productos
                                columns.ConstantColumn(80);   // Total
                            });

                            // Encabezado de tabla
                            table.Header(header =>
                            {
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Fecha").Bold().FontSize(9);
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Proveedor").Bold().FontSize(9);
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Productos").Bold().FontSize(9);
                                header.Cell().Background(Colors.Grey.Lighten2).Padding(5).Text("Total").Bold().FontSize(9);
                            });

                            // Filas de datos
                            foreach (var c in compras)
                            {
                                // Limitar texto de productos si es muy largo
                                var productosTexto = c.Productos.Length > 80
                                    ? c.Productos.Substring(0, 80) + "..."
                                    : c.Productos;

                                table.Cell().Padding(4).Text(c.Fecha.ToString("dd/MM/yyyy")).FontSize(9);
                                table.Cell().Padding(4).Text(c.Proveedor).FontSize(9);
                                table.Cell().Padding(4).Text(productosTexto).FontSize(9);
                                table.Cell().Padding(4).AlignRight().Text($"Q {c.Total:N2}").FontSize(9);
                            }
                        });

                        // Espacio antes del total
                        col.Item().PaddingTop(15);

                        // Total general
                        col.Item().Background(Colors.Green.Lighten4).Padding(10).Border(1).BorderColor(Colors.Green.Lighten2).AlignCenter()
                            .Text($"TOTAL GENERAL: Q {totalGeneral:N2}")
                            .FontSize(12).Bold().FontColor(Colors.Green.Darken3);
                    });

                    // Footer simple
                    page.Footer().AlignCenter().Text(text =>
                    {
                        text.Span("Smartcell Company - ");
                        text.Span(DateTime.Now.ToString("dd/MM/yyyy HH:mm"));
                    });
                });
            }).GeneratePdf();

            return File(pdfBytes, "application/pdf", $"ReporteCompras_{DateTime.Now:yyyyMMdd_HHmmss}.pdf");
        }



    }
}