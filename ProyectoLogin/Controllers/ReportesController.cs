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
                        text.Span(FechaLocal.Ahora().ToString("dd/MM/yyyy HH:mm"));
                    });
                });
            }).GeneratePdf();

            return File(pdfBytes, "application/pdf", $"ReporteCompras_{FechaLocal.Ahora():ddMMyyyy_HH_mm}.pdf");
        }



        // reporte Inventario
        [Authorize(Roles = "Administrador")]
        public async Task<IActionResult> ReporteInventario(int? idCategoria, int? idProveedor, string nombre, bool pdf = false)
        {
            // Cargar listas para filtros
            ViewBag.Categorias = await _context.Categorias.OrderBy(c => c.Nombre).ToListAsync();
            ViewBag.Proveedores = await _context.Proveedores.Where(p => p.Activo).OrderBy(p => p.Nombre).ToListAsync();
            ViewBag.IdCategoria = idCategoria;
            ViewBag.IdProveedor = idProveedor;
            ViewBag.Nombre = nombre ?? "";

            // Base de productos
            var productosQuery = _context.Productos.AsQueryable();

            if (idCategoria.HasValue)
                productosQuery = productosQuery.Where(p => p.IdCategoria == idCategoria.Value);

            if (!string.IsNullOrWhiteSpace(nombre))
                productosQuery = productosQuery.Where(p => p.Nombre!.Contains(nombre));

            // 🔹 Nuevo filtro real por proveedor
            if (idProveedor.HasValue)
            {
                var idsProductosProveedor = await _context.ProductosProveedores
                    .Where(pp => pp.IdProveedor == idProveedor.Value)
                    .Select(pp => pp.IdProducto)
                    .Distinct()
                    .ToListAsync();

                productosQuery = productosQuery.Where(p => idsProductosProveedor.Contains(p.IdProducto));
            }

            // Traer precios activos más recientes
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

                // Obtener proveedor principal (solo el primero)
                var prodProv = await _context.ProductosProveedores
                    .Where(pp => pp.IdProducto == p.IdProducto)
                    .Join(_context.Proveedores,
                          pp => pp.IdProveedor,
                          pr => pr.IdProveedor,
                          (pp, pr) => new { pr.IdProveedor, pr.Nombre })
                    .FirstOrDefaultAsync();

                var proveedorNombre = prodProv?.Nombre ?? "";

                decimal precioSinIVA = precio?.PrecioBase ?? 0m;
                decimal precioConIVA = precio?.PrecioVenta ?? 0m;
                int stockActual = inventario?.StockActual ?? 0;
                int stockMinimo = inventario?.StockMinimo ?? 0;
                decimal valorTotal = stockActual * precioConIVA;

                data.Add(new
                {
                    IdProducto = p.IdProducto,
                    Nombre = p.Nombre,
                    Categoria = p.Categoria?.Nombre ?? "",
                    Proveedor = proveedorNombre,
                    StockActual = stockActual,
                    StockMinimo = stockMinimo,
                    PrecioSinIVA = precioSinIVA,
                    PrecioConIVA = precioConIVA,
                    ValorTotal = valorTotal
                });
            }

            // PDF opcional
            if (pdf)
            {
                var totalInventario = data.Cast<dynamic>().Sum(d => (decimal)d.ValorTotal);

                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(25);
                        page.Size(PageSizes.A4);
                        page.Header().Text("Reporte de Inventario").FontSize(16).Bold().AlignCenter();
                        page.Content().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                                cols.RelativeColumn();
                                cols.ConstantColumn(50);
                                cols.ConstantColumn(50);
                                cols.ConstantColumn(70);
                                cols.ConstantColumn(70);
                                cols.ConstantColumn(80);
                            });

                            // Header
                            table.Header(header =>
                            {
                                header.Cell().Text("Producto").Bold();
                                header.Cell().Text("Categoría").Bold();
                                header.Cell().Text("Proveedor").Bold();
                                header.Cell().AlignCenter().Text("Stock").Bold();
                                header.Cell().AlignCenter().Text("Min").Bold();
                                header.Cell().AlignRight().Text("Sin IVA").Bold();
                                header.Cell().AlignRight().Text("Con IVA").Bold();
                                header.Cell().AlignRight().Text("Total").Bold();
                            });

                            foreach (var item in data)
                            {
                                dynamic it = item;
                                table.Cell().Text((string)it.Nombre);
                                table.Cell().Text((string)it.Categoria);
                                table.Cell().Text((string)it.Proveedor);
                                table.Cell().AlignCenter().Text(((int)it.StockActual).ToString());
                                table.Cell().AlignCenter().Text(((int)it.StockMinimo).ToString());
                                table.Cell().AlignRight().Text($"Q{((decimal)it.PrecioSinIVA):N2}");
                                table.Cell().AlignRight().Text($"Q{((decimal)it.PrecioConIVA):N2}");
                                table.Cell().AlignRight().Text($"Q{((decimal)it.ValorTotal):N2}");
                            }

                            static IContainer CellStyle(IContainer c) => c.PaddingVertical(4).PaddingHorizontal(2);
                        });

                        page.Footer().AlignRight().Text($"Valor total inventario: Q{totalInventario:N2}").Bold();
                    });
                });

                var pdfBytes = doc.GeneratePdf();
                return File(pdfBytes, "application/pdf", "ReporteInventario.pdf");
            }

            // Enviar total a la vista
            ViewBag.TotalInventario = data.Cast<dynamic>().Sum(d => (decimal)d.ValorTotal);
            return View("~/Views/Reportes/ReporteInventario.cshtml", data);
        }




        [Authorize(Roles = "Administrador,Gerente")]
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

            // 🔹 Generar PDF
            if (pdf)
            {
                var doc = Document.Create(container =>
                {
                    container.Page(page =>
                    {
                        page.Margin(25);
                        page.Size(PageSizes.A4);
                        page.Header().AlignCenter().Text("Reporte de Ajustes de Inventario").Bold().FontSize(16);

                        page.Content().Table(table =>
                        {
                            table.ColumnsDefinition(cols =>
                            {
                                cols.ConstantColumn(80);   // Fecha
                                cols.RelativeColumn(2);    // Producto
                                cols.ConstantColumn(60);   // Cantidad
                                cols.ConstantColumn(70);   // Tipo
                                cols.RelativeColumn(2);    // Motivo
                            });

                            table.Header(header =>
                            {
                                header.Cell().Text("Fecha").Bold();
                                header.Cell().Text("Producto").Bold();
                                header.Cell().AlignCenter().Text("Cantidad").Bold();
                                header.Cell().AlignCenter().Text("Tipo").Bold();
                                header.Cell().Text("Motivo").Bold();
                            });

                            foreach (var item in data)
                            {
                                table.Cell().Text(item.Fecha.ToString("dd/MM/yyyy HH:mm"));
                                table.Cell().Text(item.Producto);
                                table.Cell().AlignCenter().Text(item.Cantidad.ToString());
                                table.Cell().AlignCenter().Text(item.Tipo);
                                table.Cell().Text(item.Motivo);
                            }
                        });

                        page.Footer().AlignCenter().Text($"Generado: {FechaLocal.Ahora():dd/MM/yyyy HH:mm}");
                    });
                });

                var pdfBytes = doc.GeneratePdf();
                return File(pdfBytes, "application/pdf", $"ReporteAjustesInventario_{FechaLocal.Ahora():dd/MM/yyyy_HHmm}.pdf");
            }

            return View("~/Views/Reportes/ReporteAjustesInventario.cshtml", data);
        }






    }
}