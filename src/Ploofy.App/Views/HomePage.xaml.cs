using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>
    /// Oyundan dönüldüğünde yıldızlar güncellenmiş oluyor; her görünüşte
    /// yeniden yükleniyor.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
