using System.ComponentModel.DataAnnotations;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using SisLabTopo.Domain.Enums;
using SisLabTopo.Domain.Exceptions;
using SisLabTopo.Domain.Models;
using SisLabTopo.Services;

namespace SisLabTopo.UI.Equipos;

/// <summary>
/// ViewModel del formulario de alta/edición de un equipo. Puerto funcional de
/// <c>EquipoFormDialog.java</c>, con una mejora deliberada: usa
/// <see cref="ObservableValidator"/>/<see cref="INotifyDataErrorInfo"/> para dar
/// retroalimentación de validación POR CAMPO (Java mostraba un único
/// <c>JOptionPane</c> combinando "código y denominación son obligatorios" en un solo
/// mensaje genérico).
///
/// El código patrimonial es la clave primaria: en edición se carga pero permanece
/// deshabilitado en la vista (igual que <c>txtCodigo.setEnabled(false)</c> en Java).
/// </summary>
public partial class EquipoFormViewModel : ObservableValidator
{
    private readonly IEquipoService _equipoService;
    private readonly Equipo? _equipoExistente;

    public bool EsEdicion => _equipoExistente is not null;

    public string TituloVentana => EsEdicion ? "Editar Equipo" : "Registrar Nuevo Equipo";

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "El código patrimonial es obligatorio.")]
    private string _codigo = string.Empty;

    [ObservableProperty]
    [NotifyDataErrorInfo]
    [Required(ErrorMessage = "La denominación es obligatoria.")]
    [MaxLength(200, ErrorMessage = "La denominación no puede superar los 200 caracteres.")]
    private string _denominacion = string.Empty;

    [ObservableProperty]
    private string _marca = string.Empty;

    [ObservableProperty]
    private string _modelo = string.Empty;

    [ObservableProperty]
    private string _serie = string.Empty;

    [ObservableProperty]
    private EstadoEquipo _estadoSeleccionado = EstadoEquipoExtensions.ValorPorDefecto;

    [ObservableProperty]
    private TipoEquipo _tipoSeleccionado = TipoEquipoExtensions.ValorPorDefecto;

    [ObservableProperty]
    private string _observaciones = string.Empty;

    [ObservableProperty]
    private string _mensajeError = string.Empty;

    [ObservableProperty]
    [NotifyCanExecuteChangedFor(nameof(GuardarCommand))]
    private bool _isBusy;

    public IReadOnlyList<EstadoEquipo> EstadosDisponibles { get; } = Enum.GetValues<EstadoEquipo>();

    public IReadOnlyList<TipoEquipo> TiposDisponibles { get; } = Enum.GetValues<TipoEquipo>();

    /// <summary>Verdadero si <see cref="GuardarAsync"/> completó sin errores; lo consulta quien abrió el diálogo para decidir si debe refrescar su tabla.</summary>
    public bool GuardadoExitoso { get; private set; }

    /// <summary>La vista (Window) escucha este evento para cerrarse a sí misma; mantiene al ViewModel sin referencias a WPF.</summary>
    public event EventHandler? SolicitarCierre;

    public EquipoFormViewModel(IEquipoService equipoService, Equipo? equipoExistente)
    {
        _equipoService = equipoService;
        _equipoExistente = equipoExistente;

        if (equipoExistente is not null)
        {
            Codigo = equipoExistente.Codigo;
            Denominacion = equipoExistente.Denominacion;
            Marca = equipoExistente.Marca ?? string.Empty;
            Modelo = equipoExistente.Modelo ?? string.Empty;
            Serie = equipoExistente.Serie ?? string.Empty;
            EstadoSeleccionado = equipoExistente.Estado;
            TipoSeleccionado = equipoExistente.Tipo;
            Observaciones = equipoExistente.Observacion ?? string.Empty;
        }
    }

    private bool PuedeGuardar() => !IsBusy;

    [RelayCommand(CanExecute = nameof(PuedeGuardar))]
    private async Task GuardarAsync()
    {
        MensajeError = string.Empty;
        ValidateAllProperties();
        if (HasErrors)
        {
            return;
        }

        IsBusy = true;
        try
        {
            var equipo = _equipoExistente ?? new Equipo();
            equipo.Codigo = Codigo.Trim();
            equipo.Denominacion = Denominacion.Trim();
            equipo.Marca = Marca.Trim();
            equipo.Modelo = Modelo.Trim();
            equipo.Serie = Serie.Trim();
            equipo.Estado = EstadoSeleccionado;
            equipo.Tipo = TipoSeleccionado;
            equipo.Observacion = Observaciones.Trim();

            if (_equipoExistente is null)
            {
                equipo.Disponible = true;
                equipo.FechaRegistro = DateTime.Now;
                await _equipoService.RegistrarAsync(equipo);
            }
            else
            {
                await _equipoService.ActualizarAsync(equipo);
            }

            GuardadoExitoso = true;
            SolicitarCierre?.Invoke(this, EventArgs.Empty);
        }
        catch (ServiceException ex)
        {
            MensajeError = ex.Message;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private void Cancelar() => SolicitarCierre?.Invoke(this, EventArgs.Empty);
}
