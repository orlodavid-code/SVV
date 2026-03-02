using QuestPDF.Drawing;
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using SVV.Models;

namespace SVV.Generators
{
    public class PdfSolicitudes : IDocument
    {
        private readonly SolicitudesViaje _solicitud;
        private readonly int _duracionViaje;
        private readonly string _logoPath;
        private readonly List<TiposViatico> _tiposViatico;

        public PdfSolicitudes(
            SolicitudesViaje solicitud,
            int duracionViaje,
            string logoPath,
            List<TiposViatico> tiposViatico = null)
        {
            _solicitud = solicitud;
            _duracionViaje = duracionViaje;
            _tiposViatico = tiposViatico ?? new List<TiposViatico>();

            // Validar existencia del logo
            _logoPath = File.Exists(logoPath) ? logoPath : GetDefaultLogoPath();

            // Registrar fuente Arial si existe (opcional)
            var fontPath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "fonts", "arial.ttf");
            if (File.Exists(fontPath))
            {
                FontManager.RegisterFont(File.OpenRead(fontPath));
            }
        }

        private string GetDefaultLogoPath()
        {
            return Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "images", "logoviamtek.jpeg");
        }

        public DocumentMetadata GetMetadata() => DocumentMetadata.Default;

        public void Compose(IDocumentContainer container)
        {
            container.Page(page =>
            {
                page.Size(PageSizes.A4);
                page.Margin(40);
                page.DefaultTextStyle(x => x.FontFamily("Arial").FontSize(10).FontColor("#333333"));

                page.Header().Element(BuildHeader);
                page.Footer().Element(BuildFooter);

                page.Content().Column(col =>
                {
                    // Espaciado superior
                    col.Item().PaddingTop(10);

                    // === TARJETA DE CÓDIGO, FECHA Y ESTADO ===
                    col.Item().Background("#272d5f").Padding(12).Row(row =>
                    {
                        row.RelativeItem().Text($"Código: {_solicitud.CodigoSolicitud}").Bold().FontSize(12).FontColor("#ffffff");
                        row.RelativeItem().AlignCenter().Text($"Fecha: {DateTime.Now:dd/MM/yyyy}").Bold().FontSize(12).FontColor("#ffffff");
                        row.RelativeItem().AlignRight().Text($"Estado: {_solicitud.Estado?.Codigo ?? "PENDIENTE"}").FontSize(12)
                            .FontColor(GetColorForState(_solicitud.Estado?.Codigo)).Bold();
                    });

                    col.Item().PaddingVertical(15);

                    // === NOTIFICACIÓN DE CAMBIO DE TIPO (si existe) ===
                    if (_solicitud.FlujoAprobaciones != null)
                    {
                        var ultimoCambio = _solicitud.FlujoAprobaciones
                            .Where(f => f.Comentarios != null && f.Comentarios.Contains("Tipo de viático cambiado"))
                            .OrderByDescending(f => f.CreatedAt)
                            .FirstOrDefault();

                        if (ultimoCambio != null)
                        {
                            col.Item().Background("#e7f3ff").Padding(12).Border(1).BorderColor("#b8daff").Column(nota =>
                            {
                                nota.Item().Text("Cambio Realizado por Finanzas").Bold().FontColor("#004085");
                                nota.Item().PaddingTop(5).Text(ultimoCambio.Comentarios ?? "");
                            });
                            col.Item().PaddingVertical(10);
                        }
                    }

                    // === INFORMACIÓN GENERAL ===
                    BuildSeccion(col, "INFORMACIÓN GENERAL", inner =>
                    {
                        TwoColumnRow(inner, "Empleado:", $"{_solicitud.Empleado?.Nombre} {_solicitud.Empleado?.Apellidos}",
                                           "Proyecto:", _solicitud.NombreProyecto ?? "-");
                        TwoColumnRow(inner, "Destino:", _solicitud.Destino ?? "-",
                                           "Tipo de Viático:", _solicitud.TipoViatico?.Nombre ?? "-");
                        TwoColumnRow(inner, "Duración:", $"{_duracionViaje} días",
                                           "Monto Anticipo:", (_solicitud.MontoAnticipo?.ToString("C") ?? "N/A"));
                        SingleFullRow(inner, "Periodo:",
                            $"{(_solicitud.FechaSalida != DateOnly.MinValue ? _solicitud.FechaSalida.ToString("dd/MM/yyyy") : "-")} al " +
                            $"{(_solicitud.FechaRegreso != DateOnly.MinValue ? _solicitud.FechaRegreso.ToString("dd/MM/yyyy") : "-")}");
                    });

                    // === DETALLES DEL VIAJE ===
                    BuildSeccion(col, "DETALLES DEL VIAJE", inner =>
                    {
                        SingleFullRow(inner, "Motivo del Viaje:", _solicitud.Motivo ?? "-");
                        if (!string.IsNullOrEmpty(_solicitud.DireccionEmpresa))
                            SingleFullRow(inner, "Dirección Destino:", _solicitud.DireccionEmpresa);
                    });

                    // === INFORMACIÓN DE TRANSPORTE ===
                    BuildSeccion(col, "INFORMACIÓN DE TRANSPORTE", inner =>
                    {
                        TwoColumnRow(inner, "Medio Traslado Principal:", _solicitud.MedioTrasladoPrincipal ?? "-",
                                           "Requiere Taxi Domicilio:", _solicitud.RequiereTaxiDomicilio == true ? "Sí" : "No");

                        if (!string.IsNullOrEmpty(_solicitud.DireccionTaxiOrigen))
                            SingleFullRow(inner, "Dirección Origen Taxi:", _solicitud.DireccionTaxiOrigen);
                        if (!string.IsNullOrEmpty(_solicitud.DireccionTaxiDestino))
                            SingleFullRow(inner, "Dirección Destino Taxi:", _solicitud.DireccionTaxiDestino);
                    });

                    // === INFORMACIÓN DE HOSPEDAJE ===
                    BuildSeccion(col, "INFORMACIÓN DE HOSPEDAJE", inner =>
                    {
                        TwoColumnRow(inner, "Requiere Hospedaje:", _solicitud.RequiereHospedaje == true ? "Sí" : "No",
                                           "Noches Hospedaje:", _solicitud.NochesHospedaje?.ToString() ?? "-");
                    });

                    // === INFORMACIÓN ADICIONAL ===
                    BuildSeccion(col, "INFORMACIÓN ADICIONAL", inner =>
                    {
                        if (!string.IsNullOrEmpty(_solicitud.EmpresaVisitada))
                            TwoColumnRow(inner, "Empresa Visitada:", _solicitud.EmpresaVisitada,
                                               "Lugar Comisión Detallado:", _solicitud.LugarComisionDetallado ?? "-");
                        if (_solicitud.NumeroPersonas > 0)
                            TwoColumnRow(inner, "Número de Personas:", _solicitud.NumeroPersonas.ToString(),
                                               "Colaboradores:", _solicitud.Colaboradores ?? "-");
                    });

                    // === ANTICIPOS ===
                    if (_solicitud.Anticipos != null && _solicitud.Anticipos.Any())
                    {
                        BuildSeccion(col, "ANTICIPOS SOLICITADOS", inner =>
                        {
                            inner.Item().Table(table =>
                            {
                                table.ColumnsDefinition(columns =>
                                {
                                    columns.ConstantColumn(40);
                                    columns.RelativeColumn();
                                    columns.ConstantColumn(100);
                                });

                                table.Header(header =>
                                {
                                    header.Cell().Background("#2c3e50").Padding(8).Text("#").FontColor("#ffffff").Bold();
                                    header.Cell().Background("#2c3e50").Padding(8).Text("Monto Solicitado").FontColor("#ffffff").Bold();
                                    header.Cell().Background("#2c3e50").Padding(8).Text("Estado").FontColor("#ffffff").Bold();
                                });

                                int index = 1;
                                decimal total = 0;
                                foreach (var anticipo in _solicitud.Anticipos)
                                {
                                    var bgColor = index % 2 == 0 ? "#f8f9fa" : "#ffffff";
                                    table.Cell().Background(bgColor).Padding(8).Text(index.ToString());
                                    table.Cell().Background(bgColor).Padding(8).Text(anticipo.MontoSolicitado.ToString("C"));
                                    table.Cell().Background(bgColor).Padding(8).Text(anticipo.Estado ?? "-");
                                    total += anticipo.MontoSolicitado;
                                    index++;
                                }

                                table.Footer(footer =>
                                {
                                    footer.Cell().ColumnSpan(2).Background("#e9ecef").Padding(8).AlignRight().Text("Total:").Bold();
                                    footer.Cell().Background("#e9ecef").Padding(8).Text(total.ToString("C")).Bold();
                                });
                            });
                        });
                    }

                    // === INSTRUCCIONES Y OBSERVACIONES ===
                    BuildSeccion(col, "INSTRUCCIONES Y OBSERVACIONES", inner =>
                    {
                        inner.Item().Text("Plazo para comprobación: 5 días hábiles posteriores al término de la comisión.")
                            .FontSize(9);
                        inner.Item().Text("Documentación requerida: Facturas, tickets, comprobantes en formato PDF o XLS.")
                            .FontSize(9);
                        inner.Item().Text($"Periodo del viaje: {_duracionViaje} día(s) del " +
                            $"{(_solicitud.FechaSalida != DateOnly.MinValue ? _solicitud.FechaSalida.ToString("dd/MM/yyyy") : "-")} al " +
                            $"{(_solicitud.FechaRegreso != DateOnly.MinValue ? _solicitud.FechaRegreso.ToString("dd/MM/yyyy") : "-")}.")
                            .FontSize(9);
                    });

                    // === FIRMAS ===
                    col.Item().PaddingTop(30);
                    col.Item().Row(row =>
                    {
                        // Solicitante
                        row.RelativeItem().Border(1).BorderColor("#dee2e6").Padding(15).Height(100).Column(f =>
                        {
                            f.Item().AlignCenter().Text("Solicitante").Bold().FontSize(11);
                            f.Item().PaddingTop(25).AlignCenter().Text("Nombre y Firma").FontSize(9).FontColor("#6c757d");
                        });

                        // Jefe Inmediato
                        row.RelativeItem().Border(1).BorderColor("#dee2e6").Padding(15).Height(100).Column(f =>
                        {
                            f.Item().AlignCenter().Text("Jefe Inmediato").Bold().FontSize(11);
                            f.Item().PaddingTop(25).AlignCenter().Text("Nombre y Firma").FontSize(9).FontColor("#6c757d");
                        });

                        // Finanzas
                        row.RelativeItem().Border(1).BorderColor("#dee2e6").Padding(15).Height(100).Column(f =>
                        {
                            f.Item().AlignCenter().Text("Finanzas").Bold().FontSize(11);
                            f.Item().PaddingTop(25).AlignCenter().Text("Nombre y Firma").FontSize(9).FontColor("#6c757d");
                        });
                    });

                    // === FECHA DE GENERACIÓN ===
                    col.Item().PaddingTop(20);
                    col.Item().AlignLeft().Text($"Documento generado el {DateTime.Now:dd/MM/yyyy a las HH:mm}")
                        .FontSize(8).FontColor("#adb5bd");
                });
            });
        }

        // Construcción de una sección con título en barra oscura y contenido en fondo claro
        private void BuildSeccion(ColumnDescriptor col, string titulo, Action<ColumnDescriptor> contenido)
        {
            col.Item().Column(seccion =>
            {
                // Título de la sección (fondo oscuro)
                seccion.Item().Background("#272d5f").Padding(10).Text(titulo).FontColor("#ffffff").Bold().FontSize(12);

                // Contenido de la sección (fondo claro con borde)
                seccion.Item().Background("#ffffff").Border(1).BorderColor("#dee2e6").Padding(15).Column(contenido);
            });
            col.Item().PaddingVertical(8);
        }

        // Fila de dos columnas (label y valor)
        private void TwoColumnRow(ColumnDescriptor col, string label1, string valor1, string label2, string valor2)
        {
            col.Item().Row(row =>
            {
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(label1).Bold().FontSize(10).FontColor("#495057");
                    c.Item().Text(valor1 ?? "-").FontSize(10).FontColor("#212529");
                });
                row.RelativeItem().Column(c =>
                {
                    c.Item().Text(label2).Bold().FontSize(10).FontColor("#495057");
                    c.Item().Text(valor2 ?? "-").FontSize(10).FontColor("#212529");
                });
            });
            col.Item().PaddingBottom(8);
        }

        // Fila de una columna (label y valor)
        private void SingleFullRow(ColumnDescriptor col, string label, string valor)
        {
            col.Item().Column(c =>
            {
                c.Item().Text(label).Bold().FontSize(10).FontColor("#495057");
                c.Item().Text(valor ?? "-").FontSize(10).FontColor("#212529");
            });
            col.Item().PaddingBottom(8);
        }

        // Color según el estado (igual que en los badges de la web)
        private string GetColorForState(string estado)
        {
            if (string.IsNullOrEmpty(estado)) return "#6c757d";
            estado = estado.ToUpper();
            if (estado.Contains("APROBADA") || estado.Contains("COMPLETADA"))
                return "#28a745";
            if (estado.Contains("RECHAZADA") || estado.Contains("CANCELADA"))
                return "#dc3545";
            if (estado.Contains("PENDIENTE") || estado.Contains("BORRADOR") || estado.Contains("ENVIADA"))
                return "#ffc107";
            if (estado.Contains("PAGADO") || estado.Contains("PROCESO"))
                return "#17a2b8";
            return "#6c757d";
        }

        private void BuildHeader(IContainer container)
        {
            container.Row(row =>
            {
                // Logo con altura fija y centrado vertical
                row.ConstantItem(180)          // Ancho de la celda (ajusta según lo grande que quieras el logo)
                    .AlignMiddle()
                    .Height(75)                // Altura fija
                    .Image(_logoPath)
                    .FitHeight();               // Escala para que la altura sea 75px, el ancho será proporcional

                // Títulos centrados verticalmente
                row.RelativeItem()
                    .PaddingLeft(15)
                    .AlignMiddle()
                    .Column(col =>
                    {
                        col.Item().Text("CUVITEK Software S.C.")
                            .FontSize(20).Bold().FontColor("#9dbe2d");
                        col.Item().Text("SOLICITUD DE VIÁTICOS")
                            .FontSize(14).Bold().FontColor("#333333");
                        col.Item().PaddingTop(8).LineHorizontal(2).LineColor("#9dbe2d");
                    });
            });
        }

        private void BuildFooter(IContainer container)
        {
            container.Column(col =>
            {
                col.Item().LineHorizontal(1).LineColor("#dee2e6");
                col.Item().PaddingTop(5).Row(row =>
                {
                    row.RelativeItem().Text("Sistema de Viáticos - Documento generado electrónicamente")
                        .FontSize(8).FontColor("#6c757d");
                    row.ConstantItem(120).AlignRight().Text(text =>
                    {
                        text.Span("Página ").FontSize(8).FontColor("#6c757d");
                        text.CurrentPageNumber().FontSize(8).FontColor("#6c757d");
                        text.Span(" de ").FontSize(8).FontColor("#6c757d");
                        text.TotalPages().FontSize(8).FontColor("#6c757d");
                    });
                });
            });
        }
    }
}