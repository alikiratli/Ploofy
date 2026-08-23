using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class MemoryMatchPage : ContentPage
{
    private readonly MemoryMatchViewModel _viewModel;

    public MemoryMatchPage(MemoryMatchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>
    /// Sonuç ekranından "tekrar oyna" ile geri dönüldüğünde de burası
    /// çalışıyor; her görünüş yeni bir oturum demek.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Dispose();
    }
}
