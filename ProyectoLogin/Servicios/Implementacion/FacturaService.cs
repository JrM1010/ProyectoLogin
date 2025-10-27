using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using ProyectoLogin.Models.ModelosVentas;
using ProyectoLogin.Models;
using Microsoft.EntityFrameworkCore;

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

            QuestPDF.Settings.License = QuestPDF.Infrastructure.LicenseType.Community;

            var venta = await _context.Ventas
                .Include(v => v.Cliente)
                .Include(v => v.Detalles)
                    .ThenInclude(d => d.Producto)
                .FirstOrDefaultAsync(v => v.IdVenta == idVenta);

            if (venta == null)
                throw new Exception("Venta no encontrada.");

            var pdf = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(40);
                    page.Size(PageSizes.A4);

                    page.Header().Text("FACTURA SMARTCELL")
                        .SemiBold().FontSize(20).AlignCenter();

                    page.Content().Column(col =>
                    {
                        col.Item().Text($"Fecha: {venta.FechaVenta:dd/MM/yyyy HH:mm}");
                        col.Item().Text($"Cliente: {venta.Cliente?.Nombres} {venta.Cliente?.Apellidos}");
                        col.Item().Text($"NIT: {venta.Cliente?.Nit ?? "CF"}");
                        col.Item().Text("");

                        // Tabla de productos
                        col.Item().Table(tabla =>
                        {
                            tabla.ColumnsDefinition(c =>
                            {
                                c.ConstantColumn(40); // #
                                c.RelativeColumn(3); // Producto
                                c.RelativeColumn(1); // Cantidad
                                c.RelativeColumn(1); // Precio
                                c.RelativeColumn(1); // Subtotal
                            });

                            // Encabezado
                            tabla.Header(h =>
                            {
                                h.Cell().Text("#").Bold();
                                h.Cell().Text("Producto").Bold();
                                h.Cell().Text("Cant.").Bold();
                                h.Cell().Text("P.Unit").Bold();
                                h.Cell().Text("Subtotal").Bold();
                            });

                            int i = 1;
                            foreach (var det in venta.Detalles)
                            {
                                tabla.Cell().Text(i++);
                                tabla.Cell().Text(det.Producto?.Nombre ?? "(kit)");
                                tabla.Cell().Text($"{det.Cantidad}");
                                tabla.Cell().Text($"{det.PrecioUnitario:C}");
                                tabla.Cell().Text($"{det.Subtotal:C}");
                            }
                        });

                        col.Item().LineHorizontal(1);

                        // Totales
                        col.Item().AlignRight().Column(tot =>
                        {
                            tot.Item().Text($"Subtotal: {venta.Subtotal:C}");
                            tot.Item().Text($"IVA (12%): {venta.IVA:C}");
                            tot.Item().Text($"Total: {venta.Total:C}").Bold();
                        });
                    });

                    page.Footer()
                        .AlignCenter()
                        .Text("Gracias por su compra - SmartCell");
                });
            }).GeneratePdf();

            return pdf;
        }

    }
}
