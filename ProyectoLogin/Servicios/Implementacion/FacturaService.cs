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

        public async Task<byte[]> GenerarFacturaAsync(int idVenta)
        {
            // Licencia Community (obligatorio antes de generar)
            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var venta = await _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .Include(v => v.Usuario) // opcional: para mostrar quién atendió (si tienes la relación)
                .FirstOrDefaultAsync(v => v.IdVenta == idVenta);

            if (venta == null)
                throw new Exception("Venta no encontrada.");

            // helper para formatear moneda local (Q)
            string Moneda(decimal valor) => $"Q{valor.ToString("N2", CultureInfo.InvariantCulture)}";

            // Intentar cargar logo si existe (opcional)
            string logoPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logo.png");
            byte[]? logoBytes = null;
            if (File.Exists(logoPath))
            {
                logoBytes = await File.ReadAllBytesAsync(logoPath);
            }

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(25);
                    page.DefaultTextStyle(x => x.FontSize(10));

                    // Header con logo + datos empresa + metadatos factura
                    page.Header().Row(row =>
                    {
                        row.RelativeColumn().Stack(stack =>
                        {
                            if (logoBytes != null)
                            {
                                stack.Item().Height(60).AlignLeft().Image(logoBytes);
                            }
                            else
                            {
                                stack.Item().Text("Smartcell").FontSize(22).SemiBold();
                            }

                            stack.Item().Text("SmartcellCompany").FontSize(10);
                            stack.Item().Text("Tel: +502 1234-5678 | smartcellcompany001@gmail.com").FontSize(9).SemiBold();
                            stack.Item().Text("Dirección: Zona 1, Ciudad, Guatemala").FontSize(9);
                        });

                        row.ConstantColumn(260).Stack(meta =>
                        {
                            meta.Item().AlignRight().Text("FACTURA").FontSize(18).SemiBold();
                            meta.Item().PaddingTop(5).Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(120);
                                });

                                table.Cell().Column(c =>
                                {
                                    c.Item().Text("N° Factura").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    c.Item().Text(venta.NumeroFactura ?? "-").FontSize(11).SemiBold();
                                });

                                table.Cell().Column(c =>
                                {
                                    c.Item().Text("Fecha emisión").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    c.Item().Text(venta.FechaVenta.ToString("dd/MM/yyyy HH:mm")).FontSize(11);
                                });

                                table.Cell().Column(c =>
                                {
                                    c.Item().Text("N° Venta").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    c.Item().Text(venta.NumeroVenta ?? "-").FontSize(11);
                                });

                                table.Cell().Column(c =>
                                {
                                    c.Item().Text("Atendió").FontSize(9).FontColor(Colors.Grey.Darken1);
                                    var nombreUsuario = venta.Usuario?.NombreUsuario ?? "-";
                                    c.Item().Text(nombreUsuario).FontSize(11);
                                });
                            });
                        });
                    });

                    page.Content().Column(col =>
                    {
                        col.Spacing(10);

                        // Datos del receptor (cliente)
                        col.Item().Row(r =>
                        {
                            r.RelativeColumn().Stack(clienteStack =>
                            {
                                clienteStack.Item().Text("Datos del receptor").FontSize(12).SemiBold();
                                clienteStack.Item().Text($"Nombre: {(venta.Cliente != null ? $"{venta.Cliente.Nombres} {venta.Cliente.Apellidos}".Trim() : "Consumidor Final (CF)")}");
                                clienteStack.Item().Text($"NIT: {venta.Cliente?.Nit ?? "CF"}");
                                clienteStack.Item().Text($"Dirección: {venta.Cliente?.Direccion ?? "-"}");
                                if (!string.IsNullOrWhiteSpace(venta.Cliente?.Correo))
                                    clienteStack.Item().Text($"Correo: {venta.Cliente.Correo}");
                            });
                        });

                        

                        // Tabla de productos: # | Producto | Cant. | P.Unit con IVA | Subtotal (con IVA)
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(30);           // #
                                c.RelativeColumn(4);           // Producto
                                c.ConstantColumn(50);          // Cant.
                                c.ConstantColumn(90);          // P.Unit con IVA
                                c.ConstantColumn(90);          // Subtotal (con IVA)
                            });

                            // Encabezado
                            table.Header(header =>
                            {
                                header.Cell().Element(CellStyleHeader).Text("#");
                                header.Cell().Element(CellStyleHeader).Text("Producto");
                                header.Cell().Element(CellStyleHeader).AlignCenter().Text("Cant.");
                                header.Cell().Element(CellStyleHeader).AlignRight().Text("P.Unit con IVA");
                                header.Cell().Element(CellStyleHeader).AlignRight().Text("Subtotal (IVA)");
                            });

                            int index = 1;
                            foreach (var det in venta.Detalles)
                            {
                                decimal precioUnitario = det.PrecioUnitario;
                                decimal cantidad = det.Cantidad;
                                decimal descuentoPct = det.Descuento;
                                decimal factorDescuento = 1 - (descuentoPct / 100m);

                                // P.Unit con IVA y subtotal con IVA (redondeados a 2 decimales)
                                decimal pUnitConIva = Math.Round(precioUnitario * 1.12m, 2);
                                decimal lineaSubtotalConIva = Math.Round(precioUnitario * factorDescuento * cantidad * 1.12m, 2);

                                table.Cell().Element(CellStyle).Text(index.ToString());
                                table.Cell().Element(CellStyle).Text(det.Producto?.Nombre ?? "(KIT)");
                                table.Cell().Element(CellStyle).AlignCenter().Text(cantidad.ToString("N0"));
                                table.Cell().Element(CellStyle).AlignRight().Text(Moneda(pUnitConIva));
                                table.Cell().Element(CellStyle).AlignRight().Text(Moneda(lineaSubtotalConIva));

                                index++;
                            }

                            // estilos locales para celdas del header y cuerpo
                            static IContainer CellStyleHeader(IContainer c)
                            {
                                return c.Padding(6f).Background(Colors.Grey.Lighten3).BorderBottom(1f).BorderColor(Colors.Grey.Lighten2).Height(26f).AlignMiddle();
                            }

                            static IContainer CellStyle(IContainer c)
                            {
                                return c.Padding(6f).BorderBottom(0f).Height(24f).AlignMiddle();
                            }
                        });

                        // Totales (alineado a la derecha)
                        col.Item().PaddingTop(10f).Row(rowTot =>
                        {
                            rowTot.RelativeColumn().Stack(s => { /* espacio a la izquierda */ });

                            rowTot.ConstantColumn(260).Column(tot =>
                            {
                                tot.Item().Row(r =>
                                {
                                    r.RelativeColumn().Text("Subtotal (sin IVA):").FontSize(10);
                                    r.ConstantColumn(120).AlignRight().Text(Moneda(venta.Subtotal)).FontSize(10);
                                });

                                tot.Item().Row(r =>
                                {
                                    r.RelativeColumn().Text("IVA (12%):").FontSize(10);
                                    r.ConstantColumn(120).AlignRight().Text(Moneda(venta.IVA)).FontSize(10);
                                });

                                tot.Item().LineHorizontal(1f);

                                tot.Item().Row(r =>
                                {
                                    r.RelativeColumn().Text("Total:").FontSize(12).SemiBold();
                                    r.ConstantColumn(120).AlignRight().Text(Moneda(venta.Total)).FontSize(12).SemiBold();
                                });
                            });
                        });

                        // Pie con notas y condiciones
                        col.Item().PaddingTop(12f).Column(notes =>
                        {
                            notes.Item().Text("Condiciones:").FontSize(9).SemiBold();
                            notes.Item().Text("Factura generada electrónicamente.").FontSize(9);
                            notes.Item().Text("Si tiene dudas sobre la factura, contacte a soporte: smartcellcompany001@gmail.com / +502 1234-5678").FontSize(9);
                        });
                    });

                    // Footer
                    page.Footer()
                        .AlignCenter()
                        .Text(x =>
                        {
                            x.Span("Smartcell © ").FontSize(9);
                            x.Span(DateTime.Now.Year.ToString()).FontSize(9).SemiBold();
                            x.Span(" - Gracias por su compra").FontSize(9);
                        });
                });
            });

            using var ms = new MemoryStream();
            document.GeneratePdf(ms);
            return ms.ToArray();
        }
    }
}
