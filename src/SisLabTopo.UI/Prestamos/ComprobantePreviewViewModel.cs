using System.IO;
using System.Windows.Media.Imaging;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using SisLabTopo.Domain.Models;
using SisLabTopo.Reports;

namespace SisLabTopo.UI.Prestamos;

/// <summary>
/// ViewModel del diálogo de vista previa del comprobante PDF. Puerto funcional de
/// <c>ComprobantePreviewDialog.java</c>, que usaba PDFBox
/// (<c>PDFRenderer.renderImageWithDPI</c>) para rasterizar la primera página a una
/// imagen Swing. En .NET, PDFBox no existe; se eligió el paquete
/// <c>PDFtoImage</c> (basado en PDFium vía SkiaSharp -- SkiaSharp ya es una dependencia
/// transitiva de QuestPDF en <c>SisLabTopo.Reports</c>, así que no se introduce ningún
/// motor gráfico nuevo) porque expone exactamente la operación necesaria
/// (<c>Conversion.ToImage(byte[], page, ...)</c> -&gt; <c>SKBitmap</c>) sin tener que
/// levantar un <c>WebView2</c>/navegador embebido solo para mostrar una imagen estática.
/// El <c>SKBitmap</c> se codifica a PNG en memoria y se carga como
/// <see cref="BitmapImage"/> para que la vista lo muestre en un <c>Image</c> WPF nativo.
/// </summary>
public partial class ComprobantePreviewViewModel : ObservableObject
{
    private readonly IComprobantePrestamoGenerator _generator;
    private readonly Prestamo _prestamo;
    private readonly IReadOnlyList<DetallePrestamo> _detalles;
    private readonly IReadOnlyList<Equipo> _equipos;
    private readonly ILogger? _logger;

    [ObservableProperty]
    private BitmapSource? _vistaPrevia;

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(ImprimirCommand))]
    private bool _isBusy;

    public event EventHandler? SolicitarCierre;

    public ComprobantePreviewViewModel(
        IComprobantePrestamoGenerator generator,
        Prestamo prestamo,
        IReadOnlyList<DetallePrestamo> detalles,
        IReadOnlyList<Equipo> equipos,
        ILogger? logger = null)
    {
        _generator = generator;
        _prestamo = prestamo;
        _detalles = detalles;
        _equipos = equipos;
        _logger = logger;
    }

    /// <summary>Genera el PDF y rasteriza su primera página a 150 DPI (mismo DPI que usaba <c>PDFBoxComprobantePrinter</c> en Java).</summary>
    public async Task CargarAsync(CancellationToken ct = default)
    {
        IsBusy = true;
        MensajeError = string.Empty;
        try
        {
            var rutaPdf = await _generator.GenerarPdfAsync(_prestamo, _detalles, _equipos, ct);
            var bytesPdf = await File.ReadAllBytesAsync(rutaPdf, ct);

            using var bitmap = PDFtoImage.Conversion.ToImage(bytesPdf, page: 0, options: new PDFtoImage.RenderOptions(Dpi: 150));
            using var imagenSkia = SkiaSharp.SKImage.FromBitmap(bitmap);
            using var datosPng = imagenSkia.Encode(SkiaSharp.SKEncodedImageFormat.Png, 100);
            using var stream = new MemoryStream(datosPng.ToArray());

            var bitmapImage = new BitmapImage();
            bitmapImage.BeginInit();
            bitmapImage.CacheOption = BitmapCacheOption.OnLoad;
            bitmapImage.StreamSource = stream;
            bitmapImage.EndInit();
            bitmapImage.Freeze();

            VistaPrevia = bitmapImage;
        }
        catch (Exception ex)
        {
            _logger?.LogError(ex, "Fallo al previsualizar el PDF de comprobante.");
            MensajeError = "Error al renderizar el documento PDF.";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private bool PuedeImprimir() => !IsBusy;

    [RelayCommand(CanExecute = nameof(PuedeImprimir))]
    private async Task ImprimirAsync()
    {
        IsBusy = true;
        try
        {
            await _generator.ImprimirAsync(_prestamo, _detalles, _equipos);
            SolicitarCierre?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            MensajeError = $"Error al imprimir: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cerrar() => SolicitarCierre?.Invoke(this, EventArgs.Empty);
}
