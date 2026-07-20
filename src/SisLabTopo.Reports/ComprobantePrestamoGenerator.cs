using System.Globalization;
using System.Reflection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SkiaSharp;

namespace SisLabTopo.Reports;

/// <summary>
/// Generador del comprobante PDF de préstamo, implementado con QuestPDF (elemento Canvas
/// respaldado por SkiaSharp). Reproduce, coordenada por coordenada, el layout que generaba
/// <c>PDFBoxComprobantePrinter</c> (Java/Apache PDFBox): misma hoja A4, mismos márgenes,
/// mismo grid de datos del solicitante, misma tabla fija de 7 filas, misma línea de fecha,
/// firmas y observaciones.
/// </summary>
public sealed class ComprobantePrestamoGenerator : IComprobantePrestamoGenerator
{
    // ===== Márgenes y geometría de la hoja A4 (595 x 842 pt) — idénticos al Java original =====
    private const float PageWidth = 595f;
    private const float PageHeight = 842f;
    private const float Left = 40f;
    private const float Right = 555f;
    private const float Center = (Left + Right) / 2f;
    private const int FilasTabla = 7;
    private const float LineWidth = 0.8f;

    private const string Institucion = "Universidad Nacional del Altiplano";
    private const string Facultad = "FACULTAD DE INGENIERÍA DE MINAS";
    private const string Acreditacion = "ESCUELA ACREDITADA POR ICACIT";
    private const string LemaAnio = "\"Año de la Esperanza y el Fortalecimiento de la Democracia\"";

    private readonly ILogger<ComprobantePrestamoGenerator> _logger;

    private static readonly Lazy<SKTypeface> FontRegularLazy = new(() => CargarTipografia("LiberationSans-Regular.ttf", SKFontStyle.Normal));
    private static readonly Lazy<SKTypeface> FontBoldLazy = new(() => CargarTipografia("LiberationSans-Bold.ttf", SKFontStyle.Bold));
    private static readonly Lazy<SKTypeface> FontItalicLazy = new(() => CargarTipografia("LiberationSans-Italic.ttf", SKFontStyle.Italic));

    static ComprobantePrestamoGenerator()
    {
        QuestPDF.Settings.License = LicenseType.Community;
    }

    public ComprobantePrestamoGenerator(ILogger<ComprobantePrestamoGenerator>? logger = null)
    {
        _logger = logger ?? NullLogger<ComprobantePrestamoGenerator>.Instance;
    }

    public Task<string> GenerarPdfAsync(
        Prestamo prestamo,
        IReadOnlyList<DetallePrestamo> detalles,
        IReadOnlyList<Equipo> equipos,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(prestamo);
        ArgumentNullException.ThrowIfNull(detalles);
        ArgumentNullException.ThrowIfNull(equipos);

        return Task.Run(() => GenerarPdfCore(prestamo, detalles, equipos), cancellationToken);
    }

    public async Task ImprimirAsync(
        Prestamo prestamo,
        IReadOnlyList<DetallePrestamo> detalles,
        IReadOnlyList<Equipo> equipos,
        CancellationToken cancellationToken = default)
    {
        var rutaPdf = await GenerarPdfAsync(prestamo, detalles, equipos, cancellationToken).ConfigureAwait(false);

        try
        {
            var startInfo = new System.Diagnostics.ProcessStartInfo
            {
                FileName = rutaPdf,
                UseShellExecute = true,
                Verb = "print",
            };
            using var process = System.Diagnostics.Process.Start(startInfo);
            _logger.LogInformation("Comprobante enviado a imprimir mediante el verbo del sistema operativo: {RutaPdf}", rutaPdf);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Fallo durante el envío de impresión física del comprobante {PrestamoId}", prestamo.Id);
            throw new PrintException("No se pudo enviar el documento a la impresora del sistema.", ex);
        }
    }

    // ============================================================
    // Generación del documento
    // ============================================================

    private string GenerarPdfCore(Prestamo prestamo, IReadOnlyList<DetallePrestamo> detalles, IReadOnlyList<Equipo> equipos)
    {
        try
        {
            // Precargar TODOS los equipos una sola vez (evita una lectura por cada fila de la tabla).
            var codigoANombre = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (var eq in equipos)
            {
                if (!string.IsNullOrEmpty(eq.Codigo))
                {
                    codigoANombre[eq.Codigo.Trim()] = eq.Denominacion;
                }
            }

            var fileName = $"comprobante_prestamo_{prestamo.Id}_{Guid.NewGuid():N}.pdf";
            var tempPath = Path.Combine(Path.GetTempPath(), fileName);

            var documento = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageWidth, PageHeight, Unit.Point);
                    page.Margin(0, Unit.Point);
                    page.PageColor(QuestPDF.Helpers.Colors.White);
                    page.Content().Canvas((canvas, _) =>
                    {
                        var logoUna = CargarLogo("Logo_UNAP.png");
                        var logoMinas = CargarLogo("Logo_Minas.png");
                        try
                        {
                            DibujarComprobante(canvas, prestamo, detalles, codigoANombre, logoUna, logoMinas);
                        }
                        finally
                        {
                            logoUna?.Dispose();
                            logoMinas?.Dispose();
                        }
                    });
                });
            });

            documento.GeneratePdf(tempPath);
            return tempPath;
        }
        catch (Exception ex) when (ex is not PrintException)
        {
            _logger.LogError(ex, "Fallo al generar el archivo PDF de comprobante para el préstamo {PrestamoId}", prestamo.Id);
            throw new PrintException("Error al escribir el comprobante en formato PDF.", ex);
        }
    }

    private void DibujarComprobante(
        SKCanvas canvas,
        Prestamo prestamo,
        IReadOnlyList<DetallePrestamo> detalles,
        IReadOnlyDictionary<string, string> codigoANombre,
        SKBitmap? logoUna,
        SKBitmap? logoMinas)
    {
        var fontBold = FontBoldLazy.Value;
        var fontNormal = FontRegularLazy.Value;
        var fontItalic = FontItalicLazy.Value;

        // ============ ENCABEZADO ============
        const float headTop = 812f;
        const float headBottom = 750f;
        DrawRect(canvas, Left, headBottom, Right - Left, headTop - headBottom);

        const float logoSize = 52f;
        var logoY = headBottom + ((headTop - headBottom) - logoSize) / 2f;
        if (logoUna is not null)
        {
            DrawImage(canvas, logoUna, Left + 10, logoY, logoSize, logoSize);
        }

        if (logoMinas is not null)
        {
            DrawImage(canvas, logoMinas, Right - 10 - logoSize, logoY, logoSize, logoSize);
        }

        DrawCentered(canvas, fontBold, 14, Center, 793, Institucion);
        DrawCentered(canvas, fontBold, 11, Center, 777, Facultad);
        DrawCentered(canvas, fontBold, 9, Center, 762, Acreditacion);
        DrawCentered(canvas, fontItalic, 8.5f, Center, 740, LemaAnio);

        // ============ DATOS DEL SOLICITANTE (recuadros) ============
        var y = 725f;
        const float cellH = 20f;

        DrawRect(canvas, Left, y - cellH, Right - Left, cellH);
        DrawLabelValue(canvas, fontBold, fontNormal, Left + 6, y - 13, "Docente Responsable:", prestamo.Docente);
        y -= cellH;

        const float cA = Left, cB = 300f, cC = 415f;
        DrawRect(canvas, Left, y - cellH, Right - Left, cellH);
        DrawLine(canvas, cB, y - cellH, cB, y);
        DrawLine(canvas, cC, y - cellH, cC, y);
        DrawLabelValue(canvas, fontBold, fontNormal, cA + 6, y - 13, "Curso:", prestamo.Curso);
        DrawLabelValue(canvas, fontBold, fontNormal, cB + 6, y - 13, "Semestre:", prestamo.Semestre);
        DrawLabelValue(canvas, fontBold, fontNormal, cC + 6, y - 13, "Cód. Estudiante:", prestamo.CodigoEstudiante);
        y -= cellH;

        DrawRect(canvas, Left, y - cellH, Right - Left, cellH);
        DrawLabelValue(canvas, fontBold, fontNormal, Left + 6, y - 13, "Nombre del Estudiante:", prestamo.NombreEstudiante);
        y -= cellH;

        // ============ TEXTO DE COMPROMISO ============
        y -= 22;
        DrawText(canvas, fontNormal, 9.5f, Left, y, "Por el presente, hago constar que recibí los siguientes equipos, instrumentos y otros del");
        DrawText(canvas, fontNormal, 9.5f, Left, y - 13, "Laboratorio de Topografía Minera de la FIM, para ser utilizados dentro del campus");
        DrawText(canvas, fontNormal, 9.5f, Left, y - 26, "universitario, por lo que bajo responsabilidad, firmamos la presente.");

        // ============ TABLA DE EQUIPOS (7 filas, 3 columnas) ============
        var tableTop = y - 44;
        const float headerH = 20f;
        const float rowH = 24f;
        const float colNum = Left + 34f;
        const float colCodigo = Right - 130f;
        var tableBottom = tableTop - headerH - (FilasTabla * rowH);

        DrawCentered(canvas, fontBold, 9.5f, (Left + colNum) / 2f, tableTop - 14, "N°");
        DrawCentered(canvas, fontBold, 9.5f, (colNum + colCodigo) / 2f, tableTop - 14, "Descripción del Equipo");
        DrawCentered(canvas, fontBold, 9.5f, (colCodigo + Right) / 2f, tableTop - 14, "Código Patrimonial");

        DrawLine(canvas, Left, tableTop, Right, tableTop);
        DrawLine(canvas, Left, tableTop - headerH, Right, tableTop - headerH);
        for (var i = 0; i < FilasTabla; i++)
        {
            var rowY = tableTop - headerH - ((i + 1) * rowH);
            DrawLine(canvas, Left, rowY, Right, rowY);
        }

        DrawLine(canvas, Left, tableTop, Left, tableBottom);
        DrawLine(canvas, colNum, tableTop, colNum, tableBottom);
        DrawLine(canvas, colCodigo, tableTop, colCodigo, tableBottom);
        DrawLine(canvas, Right, tableTop, Right, tableBottom);

        for (var i = 0; i < FilasTabla; i++)
        {
            var rowY = tableTop - headerH - (i * rowH);
            var textY = rowY - 15;
            DrawCentered(canvas, fontNormal, 9.5f, (Left + colNum) / 2f, textY, (i + 1).ToString(CultureInfo.InvariantCulture));

            if (i < detalles.Count)
            {
                var det = detalles[i];
                var cod = det.EquipoCodigo ?? string.Empty;
                var nombre = codigoANombre.TryGetValue(cod.Trim(), out var denominacion) ? denominacion : "Equipo";
                DrawText(canvas, fontNormal, 9.5f, colNum + 8, textY, Recortar(nombre, fontNormal, 9.5f, colCodigo - colNum - 16));
                DrawCentered(canvas, fontNormal, 9.5f, (colCodigo + Right) / 2f, textY, cod);
            }
        }

        // ============ FECHA ============
        var footerY = tableBottom - 26;
        var cultura = ResolverCulturaEsPeru();
        var fechaStr = "Puno, C.U., " + DateTime.Today.ToString("dd 'de' MMMM 'del' yyyy", cultura);
        DrawText(canvas, fontNormal, 10, Left, footerY, fechaStr);

        // ============ FIRMAS ============
        var signLineY = footerY - 70;
        const float sign1C = 150f;
        const float sign2C = 445f;
        DrawLine(canvas, sign1C - 75, signLineY, sign1C + 75, signLineY);
        DrawLine(canvas, sign2C - 75, signLineY, sign2C + 75, signLineY);
        DrawCentered(canvas, fontBold, 9.5f, sign1C, signLineY - 14, "FIRMA DEL DOCENTE");
        DrawCentered(canvas, fontBold, 9.5f, sign2C, signLineY - 14, "FIRMA DEL ESTUDIANTE");

        // ============ OBSERVACIONES ============
        var obsY = signLineY - 45;
        var obs = prestamo.Observaciones ?? string.Empty;
        DrawText(canvas, fontBold, 10, Left, obsY, "Observaciones:");
        DrawText(canvas, fontNormal, 9.5f, Left + 78, obsY, Recortar(obs, fontNormal, 9.5f, Right - (Left + 78)));
        DrawLine(canvas, Left, obsY - 16, Right, obsY - 16);
        DrawLine(canvas, Left, obsY - 32, Right, obsY - 32);
    }

    // ============================================================
    // Utilidades de dibujo (equivalentes 1:1 a los helpers de PDFBoxComprobantePrinter.java,
    // adaptadas al sistema de coordenadas de SkiaSharp: origen arriba-izquierda, Y creciendo
    // hacia abajo, en vez del origen abajo-izquierda de PDFBox. La función FlipY() es el único
    // punto de conversión entre ambos sistemas.)
    // ============================================================

    private static float FlipY(float pdfY) => PageHeight - pdfY;

    private static void DrawRect(SKCanvas canvas, float x, float y, float w, float h)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = LineWidth,
            Color = SKColors.Black,
            IsAntialias = true,
        };
        var rect = new SKRect(x, FlipY(y + h), x + w, FlipY(y));
        canvas.DrawRect(rect, paint);
    }

    private static void DrawLine(SKCanvas canvas, float x1, float y1, float x2, float y2)
    {
        using var paint = new SKPaint
        {
            Style = SKPaintStyle.Stroke,
            StrokeWidth = LineWidth,
            Color = SKColors.Black,
            IsAntialias = true,
        };
        canvas.DrawLine(x1, FlipY(y1), x2, FlipY(y2), paint);
    }

    private static void DrawImage(SKCanvas canvas, SKBitmap bitmap, float x, float y, float w, float h)
    {
        var dest = new SKRect(x, FlipY(y + h), x + w, FlipY(y));
        canvas.DrawBitmap(bitmap, dest);
    }

    private static void DrawText(SKCanvas canvas, SKTypeface typeface, float size, float x, float y, string? text)
    {
        text ??= string.Empty;
        using var paint = new SKPaint
        {
            Typeface = typeface,
            TextSize = size,
            Color = SKColors.Black,
            IsAntialias = true,
        };
        canvas.DrawText(Sanitizar(text), x, FlipY(y), paint);
    }

    /// <summary>Dibuja texto centrado horizontalmente respecto a <paramref name="centerX"/>.</summary>
    private static void DrawCentered(SKCanvas canvas, SKTypeface typeface, float size, float centerX, float y, string? text)
    {
        text ??= string.Empty;
        var safe = Sanitizar(text);
        var width = MedirAncho(safe, typeface, size);
        DrawText(canvas, typeface, size, centerX - width / 2f, y, safe);
    }

    /// <summary>Dibuja una etiqueta en negrita seguida de su valor en texto normal.</summary>
    private static void DrawLabelValue(SKCanvas canvas, SKTypeface bold, SKTypeface normal, float x, float y, string label, string? value)
    {
        DrawText(canvas, bold, 9.5f, x, y, label);
        var lw = MedirAncho(Sanitizar(label), bold, 9.5f);
        DrawText(canvas, normal, 9.5f, x + lw + 5, y, value ?? string.Empty);
    }

    private static float MedirAncho(string text, SKTypeface typeface, float size)
    {
        using var paint = new SKPaint { Typeface = typeface, TextSize = size };
        return paint.MeasureText(text);
    }

    /// <summary>Recorta el texto para que no exceda el ancho máximo disponible (en puntos), añadiendo "...".</summary>
    private static string Recortar(string? text, SKTypeface typeface, float size, float maxWidth)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        var safe = Sanitizar(text);
        if (MedirAncho(safe, typeface, size) <= maxWidth)
        {
            return safe;
        }

        const string ellipsis = "...";
        while (safe.Length > 1 && MedirAncho(safe + ellipsis, typeface, size) > maxWidth)
        {
            safe = safe[..^1];
        }

        return safe + ellipsis;
    }

    /// <summary>
    /// Reemplaza caracteres tipográficos que la fuente estándar de PDFBox (WinAnsi) no soportaba.
    /// Se conserva por paridad visual exacta con la versión Java, aunque con una fuente TrueType
    /// embebida (Liberation Sans, codificación Unicode completa) ya no es estrictamente necesario.
    /// </summary>
    private static string Sanitizar(string? text)
    {
        if (string.IsNullOrEmpty(text))
        {
            return string.Empty;
        }

        return text
            .Replace('–', '-')  // en dash
            .Replace('—', '-')  // em dash
            .Replace('‘', '\'') // comilla simple tipográfica de apertura
            .Replace('’', '\'') // comilla simple tipográfica de cierre
            .Replace('“', '"')  // comilla doble tipográfica de apertura
            .Replace('”', '"')  // comilla doble tipográfica de cierre
            .Replace('…', '.'); // puntos suspensivos
    }

    private static CultureInfo ResolverCulturaEsPeru()
    {
        try
        {
            return CultureInfo.GetCultureInfo("es-PE");
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.GetCultureInfo("es-ES");
        }
    }

    // ============================================================
    // Carga de recursos embebidos (fuentes y logos)
    // ============================================================

    private static SKTypeface CargarTipografia(string resourceFileName, SKFontStyle fallbackStyle)
    {
        var assembly = typeof(ComprobantePrestamoGenerator).Assembly;
        var resourceName = $"SisLabTopo.Reports.Assets.Fonts.{resourceFileName}";
        using var stream = assembly.GetManifestResourceStream(resourceName);
        if (stream is null)
        {
            return SKTypeface.FromFamilyName("Arial", fallbackStyle) ?? SKTypeface.Default;
        }

        using var memoryStream = new MemoryStream();
        stream.CopyTo(memoryStream);
        memoryStream.Position = 0;
        return SKTypeface.FromStream(memoryStream) ?? SKTypeface.FromFamilyName("Arial", fallbackStyle) ?? SKTypeface.Default;
    }

    private SKBitmap? CargarLogo(string resourceFileName)
    {
        var assembly = typeof(ComprobantePrestamoGenerator).Assembly;
        var resourceName = $"SisLabTopo.Reports.Assets.{resourceFileName}";
        try
        {
            using var stream = assembly.GetManifestResourceStream(resourceName);
            if (stream is null)
            {
                _logger.LogWarning("No se encontró el logo en recursos embebidos: {ResourceName}", resourceName);
                return null;
            }

            var bitmap = SKBitmap.Decode(stream);
            if (bitmap is null)
            {
                _logger.LogWarning("No se pudo decodificar el logo embebido: {ResourceName}", resourceName);
            }

            return bitmap;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "No se pudo cargar el logo {ResourceName}", resourceName);
            return null;
        }
    }
}
