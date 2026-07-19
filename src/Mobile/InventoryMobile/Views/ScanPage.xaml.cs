using InventoryMobile.ViewModels;
using ZXing.Net.Maui;

namespace InventoryMobile.Views;

public partial class ScanPage : ContentPage
{
    private readonly ScanViewModel _viewModel;

    public ScanPage(ScanViewModel viewModel)
    {
        InitializeComponent();
        _viewModel = viewModel;
        BindingContext = viewModel;

        BarcodeView.Options = new BarcodeReaderOptions { Formats = BarcodeFormats.All };
    }

    private void OnBarcodesDetected(object? sender, BarcodeDetectionEventArgs e)
    {
        var value = e.Results.FirstOrDefault()?.Value;
        if (string.IsNullOrWhiteSpace(value)) return;

        // Le callback arrive sur le thread caméra — il faut revenir sur le thread UI avant
        // de toucher un Command/ObservableProperty (contrainte MAUI/data-binding).
        MainThread.BeginInvokeOnMainThread(() =>
        {
            if (_viewModel.BarcodeDetectedCommand.CanExecute(value))
            {
                _viewModel.BarcodeDetectedCommand.Execute(value);
            }
        });
    }
}
