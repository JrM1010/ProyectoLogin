using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ProyectoLogin.Models.ModelosVentas;
using ProyectoLogin.Models;
using Microsoft.EntityFrameworkCore;
using System.IO;
using System.Globalization;
using System;

namespace ProyectoLogin.Servicios.Implementacion
{
    public class FacturaService
    {
        private readonly DbPruebaContext _context;

        public FacturaService(DbPruebaContext context)
        {
            _context = context;
        }

        [Obsolete]
        public async Task<byte[]> GenerarFacturaAsync(int idVenta)
        {
            QuestPDF.Settings.License = LicenseType.Community;

            var venta = await _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                        .ThenInclude(p => p.ProductosUnidades)
                            .ThenInclude(pu => pu.UnidadMedida)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Kit) // 🔹 INCLUIR LA INFORMACIÓN DEL KIT
                .Include(v => v.Usuario)
                .FirstOrDefaultAsync(v => v.IdVenta == idVenta);

            if (venta == null)
                throw new Exception("Venta no encontrada.");

            string Moneda(decimal valor) => $"Q{valor.ToString("N2", CultureInfo.InvariantCulture)}";

            // Colores corporativos
            var colorPrimario = Colors.Blue.Darken3;
            var colorSecundario = Colors.Grey.Darken3;
            var colorFondoHeader = Colors.Blue.Lighten5;
            var colorBorde = Colors.Grey.Lighten2;

            // Logo (si existe)
            string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
            byte[]? logoBytes = File.Exists(logoPath) ? await File.ReadAllBytesAsync(logoPath) : null;

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(30);
                    page.DefaultTextStyle(x => x.FontSize(10).FontFamily("Helvetica"));

                    // ENCABEZADO MEJORADO --------------------------------------------------
                    page.Header().Background(colorFondoHeader).Padding(15).Row(row =>
                    {
                        row.RelativeColumn().Stack(stack =>
                        {
                            if (logoBytes != null)
                                stack.Item().Height(50).AlignLeft().Image(logoBytes);
                            else
                                stack.Item().Text("SMARTCELL").FontSize(24).Bold().FontColor(colorPrimario);

                            stack.Item().PaddingTop(5).Text("Smartcell Company S.A.").FontSize(11).SemiBold().FontColor(colorSecundario);
                            stack.Item().Text("Tel: +502 1234-5678").FontSize(9);
                            stack.Item().Text("smartcellcompany001@gmail.com").FontSize(9);
                            stack.Item().Text("Zona 1, Ciudad de Guatemala").FontSize(9);
                        });

                        row.ConstantColumn(200).Stack(meta =>
                        {
                            meta.Item().Background(colorPrimario).Padding(8).AlignCenter().Text("FACTURA")
                                .FontSize(16).Bold().FontColor(Colors.White);

                            meta.Item().PaddingTop(10).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(100);
                                });

                                table.Cell().Column(c =>
                                {
                                    c.Item().Text("N° Factura").FontSize(8).FontColor(Colors.Grey.Darken2);
                                    c.Item().PaddingBottom(3).Text(venta.NumeroFactura ?? "-").FontSize(10).Bold();
                                });

                                table.Cell().Column(c =>
                                {
                                    c.Item().Text("Fecha emisión").FontSize(8).FontColor(Colors.Grey.Darken2);
                                    c.Item().PaddingBottom(3).Text(venta.FechaVenta.ToString("dd/MM/yyyy HH:mm")).FontSize(10);
                                });

                                table.Cell().Column(c =>
                                {
                                    c.Item().Text("N° Venta").FontSize(8).FontColor(Colors.Grey.Darken2);
                                    c.Item().PaddingBottom(3).Text(venta.NumeroVenta ?? "-").FontSize(10);
                                });

                                table.Cell().Column(c =>
                                {
                                    c.Item().Text("Atendió").FontSize(8).FontColor(Colors.Grey.Darken2);
                                    var nombreUsuario = venta.Usuario?.NombreUsuario ?? "-";
                                    c.Item().PaddingBottom(3).Text(nombreUsuario).FontSize(10);
                                });
                            });
                        });
                    });

                    // CONTENIDO MEJORADO --------------------------------------------------
                    page.Content().PaddingVertical(15).Stack(content =>
                    {
                        // 🔹 DATOS GENERALES CON DISEÑO MEJORADO
                        content.Item().PaddingBottom(15).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn(1);
                                columns.RelativeColumn(1);
                            });

                            // Datos del Cliente
                            table.Cell().Border(1).BorderColor(colorBorde).Padding(10).Stack(left =>
                            {
                                left.Item().PaddingBottom(5).Text("DATOS DEL CLIENTE").Bold().FontSize(11).FontColor(colorPrimario);
                                left.Item().PaddingBottom(2).Text($"Nombre: {(venta.Cliente != null ? $"{venta.Cliente.Nombres} {venta.Cliente.Apellidos}".Trim() : "Consumidor Final (CF)")}");
                                left.Item().PaddingBottom(2).Text($"NIT: {venta.Cliente?.Nit ?? "CF"}");
                                left.Item().PaddingBottom(2).Text($"Dirección: {venta.Cliente?.Direccion ?? "-"}");
                                if (!string.IsNullOrWhiteSpace(venta.Cliente?.Correo))
                                    left.Item().Text($"Correo: {venta.Cliente.Correo}");
                            });

                            // Detalles de Venta
                            table.Cell().Border(1).BorderColor(colorBorde).Padding(10).Stack(right =>
                            {
                                right.Item().PaddingBottom(5).Text("INFORMACIÓN DE VENTA").Bold().FontSize(11).FontColor(colorPrimario);
                                right.Item().PaddingBottom(2).Text($"Fecha: {venta.FechaVenta:dd/MM/yyyy HH:mm}");
                                right.Item().PaddingBottom(2).Text($"Método de pago: {venta.MetodoPago}");
                                right.Item().PaddingBottom(2).Text($"Vendedor: {venta.Usuario?.NombreUsuario ?? "-"}");
                                right.Item().Text($"Estado: {venta.Estado}");
                            });
                        });

                        // 🔹 TABLA DE PRODUCTOS MEJORADA
                        content.Item().PaddingTop(10).Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.ConstantColumn(25); // #
                                columns.RelativeColumn(3);  // Producto
                                columns.ConstantColumn(60); // Unidad
                                columns.ConstantColumn(60); // Cantidad
                                columns.ConstantColumn(70); // Precio unitario
                                columns.ConstantColumn(60); // Descuento
                                columns.ConstantColumn(80); // Subtotal
                            });

                            // Encabezado de tabla
                            table.Header(header =>
                            {
                                header.Cell().Background(colorPrimario).Padding(8).AlignCenter().Text("#").FontColor(Colors.White).Bold();
                                header.Cell().Background(colorPrimario).Padding(8).Text("PRODUCTO").FontColor(Colors.White).Bold();
                                header.Cell().Background(colorPrimario).Padding(8).AlignCenter().Text("UNIDAD").FontColor(Colors.White).Bold();
                                header.Cell().Background(colorPrimario).Padding(8).AlignCenter().Text("CANTIDAD").FontColor(Colors.White).Bold();
                                header.Cell().Background(colorPrimario).Padding(8).AlignRight().Text("P. UNIT.").FontColor(Colors.White).Bold();
                                header.Cell().Background(colorPrimario).Padding(8).AlignCenter().Text("DESC.").FontColor(Colors.White).Bold();
                                header.Cell().Background(colorPrimario).Padding(8).AlignRight().Text("SUBTOTAL").FontColor(Colors.White).Bold();
                            });

                            int index = 1;
                            foreach (var d in venta.Detalles)
                            {
                                // 🔹 CORRECCIÓN: OBTENER NOMBRE CORRECTO PARA KITS
                                string nombreProducto;
                                string unidad;

                                if (d.IdKit.HasValue && d.IdKit > 0)
                                {
                                    // Es un kit - usar el nombre del kit
                                    nombreProducto = d.Kit?.Nombre ?? "(KIT)";
                                    unidad = "Kit";
                                }
                                else
                                {
                                    // Es un producto normal
                                    nombreProducto = d.Producto?.Nombre ?? "Producto no encontrado";
                                    unidad = d.Producto?.ProductosUnidades?.FirstOrDefault()?.UnidadMedida?.Nombre ?? "Unidad";
                                }

                                var descuento = d.Descuento > 0 ? $"{d.Descuento:N2}%" : "-";
                                decimal subtotal = d.Subtotal > 0 ? d.Subtotal : Math.Round(d.Cantidad * d.PrecioUnitario * (1 - d.Descuento / 100m), 2);

                                // Filas alternadas para mejor legibilidad
                                var backgroundColor = index % 2 == 0 ? Colors.Grey.Lighten5 : Colors.White;

                                table.Cell().Background(backgroundColor).Padding(6).AlignCenter().Text(index.ToString());
                                table.Cell().Background(backgroundColor).Padding(6).Text(nombreProducto);
                                table.Cell().Background(backgroundColor).Padding(6).AlignCenter().Text(unidad);
                                table.Cell().Background(backgroundColor).Padding(6).AlignCenter().Text(d.Cantidad.ToString("N0"));
                                table.Cell().Background(backgroundColor).Padding(6).AlignRight().Text(Moneda(d.PrecioUnitario));
                                table.Cell().Background(backgroundColor).Padding(6).AlignCenter().Text(descuento);
                                table.Cell().Background(backgroundColor).Padding(6).AlignRight().Text(Moneda(subtotal));

                                index++;
                            }
                        });

                        // 🔹 RESUMEN DE TOTALES MEJORADO
                        content.Item().AlignRight().PaddingTop(20).Width(250).Table(totales =>
                        {
                            totales.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(120);
                                c.ConstantColumn(130);
                            });

                            totales.Cell().BorderBottom(1).BorderColor(colorBorde).Padding(5).Text("Subtotal:").SemiBold();
                            totales.Cell().BorderBottom(1).BorderColor(colorBorde).Padding(5).Text(Moneda(venta.Subtotal)).AlignRight();

                            totales.Cell().BorderBottom(1).BorderColor(colorBorde).Padding(5).Text("IVA (12%):").SemiBold();
                            totales.Cell().BorderBottom(1).BorderColor(colorBorde).Padding(5).Text(Moneda(venta.IVA)).AlignRight();

                            totales.Cell().Background(colorPrimario).Padding(8).Text("TOTAL:").Bold().FontColor(Colors.White);
                            totales.Cell().Background(colorPrimario).Padding(8).Text(Moneda(venta.Total)).Bold().FontColor(Colors.White).AlignRight();
                        });

                        // 🔹 DETALLE DE KITS (INFORMACIÓN ADICIONAL)
                        var kitsEnVenta = venta.Detalles.Where(d => d.IdKit.HasValue && d.IdKit > 0).ToList();
                        if (kitsEnVenta.Any())
                        {
                            content.Item().PaddingTop(25).Border(1).BorderColor(colorBorde).Padding(10).Stack(async kitsInfo =>
                            {
                                kitsInfo.Item().PaddingBottom(5).Text("COMPOSICIÓN DE KITS/PROMOCIONES").Bold().FontSize(10).FontColor(colorPrimario);

                                foreach (var detalleKit in kitsEnVenta)
                                {
                                    if (detalleKit.Kit != null)
                                    {
                                        kitsInfo.Item().PaddingTop(3).Text($"{detalleKit.Kit.Nombre} (Cantidad: {detalleKit.Cantidad})").SemiBold().FontSize(9);

                                        // Cargar los detalles del kit si no están incluidos
                                        var kitCompleto = await _context.Kits
                                            .Include(k => k.Detalles)
                                                .ThenInclude(d => d.Producto)
                                            .FirstOrDefaultAsync(k => k.IdKit == detalleKit.IdKit);

                                        if (kitCompleto?.Detalles != null)
                                        {
                                            foreach (var detalle in kitCompleto.Detalles)
                                            {
                                                kitsInfo.Item().PaddingLeft(10).Text($"• {detalle.Cantidad} x {detalle.Producto?.Nombre ?? "Producto"}").FontSize(8);
                                            }
                                        }
                                    }
                                }
                            });
                        }

                        // 🔹 INFORMACIÓN ADICIONAL
                        content.Item().PaddingTop(20).Border(1).BorderColor(colorBorde).Padding(10).Stack(info =>
                        {
                            info.Item().PaddingBottom(5).Text("INFORMACIÓN ADICIONAL").Bold().FontSize(10).FontColor(colorPrimario);
                            info.Item().Text("• Esta factura es un documento legal válido").FontSize(8);
                            info.Item().Text("• Los productos tienen garantía según políticas de la empresa").FontSize(8);
                            info.Item().Text("• Para reclamos o devoluciones presentar esta factura").FontSize(8);
                            if (kitsEnVenta.Any())
                            {
                                info.Item().Text("• Los kits/promociones no son reembolsables individualmente").FontSize(8);
                            }
                        });

                        // 🔹 PIE DE PÁGINA MEJORADO
                        content.Item().PaddingTop(20).Stack(pie =>
                        {
                            pie.Item().Background(colorFondoHeader).Padding(10).AlignCenter().Stack(mensaje =>
                            {
                                mensaje.Item().Text("¡Gracias por su preferencia!").Bold().FontSize(11).FontColor(colorPrimario);
                                mensaje.Item().PaddingTop(3).Text("Es un placer atenderle - Su satisfacción es nuestra prioridad").Italic().FontSize(9);
                            });

                            pie.Item().PaddingTop(10).AlignCenter().Text("Documento generado automáticamente por Smartcell Company")
                                .FontSize(8).FontColor(Colors.Grey.Darken2);
                        });
                    });

                    // FOOTER MEJORADO --------------------------------------------------
                    page.Footer().AlignCenter().PaddingTop(10).Stack(footer =>
                    {
                        footer.Item().BorderTop(1).BorderColor(colorBorde).PaddingTop(5).Text(x =>
                        {
                            x.Span("Smartcell Company S.A. © ").FontSize(9).FontColor(colorSecundario);
                            x.Span(DateTime.Now.Year.ToString()).FontSize(9).Bold().FontColor(colorPrimario);
                            x.Span(" - Todos los derechos reservados").FontSize(9).FontColor(colorSecundario);
                        });
                        footer.Item().Text("www.smartcellcompany.com.gt | +502 1234-5678 | smartcellcompany001@gmail.com")
                            .FontSize(7).FontColor(Colors.Grey.Darken1);
                    });
                });
            });

            using var ms = new MemoryStream();
            document.GeneratePdf(ms);
            return ms.ToArray();
        }
    }
}