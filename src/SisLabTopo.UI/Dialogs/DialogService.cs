using System.Linq;
using System.Windows;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Win32;

namespace SisLabTopo.UI.Dialogs;

/// <inheritdoc cref="IDialogService"/>
public class DialogService : IDialogService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly IReadOnlyDictionary<Type, Type> _viewModelToWindow;

    public DialogService(IServiceProvider serviceProvider, IReadOnlyDictionary<Type, Type> viewModelToWindow)
    {
        _serviceProvider = serviceProvider;
        _viewModelToWindow = viewModelToWindow;
    }

    public bool? ShowDialog<TViewModel>(TViewModel viewModel) where TViewModel : class
    {
        if (!_viewModelToWindow.TryGetValue(typeof(TViewModel), out var windowType))
        {
            throw new InvalidOperationException(
                $"No hay ninguna ventana registrada en DialogService para el ViewModel '{typeof(TViewModel).Name}'.");
        }

        var window = (Window)_serviceProvider.GetRequiredService(windowType);
        window.DataContext = viewModel;

        var owner = Application.Current.Windows.OfType<Window>().FirstOrDefault(w => w.IsActive)
                    ?? Application.Current.MainWindow;
        if (!ReferenceEquals(owner, window))
        {
            window.Owner = owner;
        }

        return window.ShowDialog();
    }

    public void ShowInfo(string message, string title = "Información") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Information);

    public void ShowError(string message, string title = "Error") =>
        MessageBox.Show(message, title, MessageBoxButton.OK, MessageBoxImage.Error);

    public bool ShowConfirm(string message, string title = "Confirmar") =>
        MessageBox.Show(message, title, MessageBoxButton.YesNo, MessageBoxImage.Question) == MessageBoxResult.Yes;

    public string? ShowOpenFileDialog(string filter, string title)
    {
        var dialog = new OpenFileDialog { Filter = filter, Title = title };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }

    public string? ShowSaveFileDialog(string filter, string title, string defaultFileName)
    {
        var dialog = new SaveFileDialog { Filter = filter, Title = title, FileName = defaultFileName };
        return dialog.ShowDialog() == true ? dialog.FileName : null;
    }
}
