using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class HuntPage : ContentPage
{
    private readonly HuntViewModel _viewModel;

    public HuntPage(HuntViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

    }

    /// <summary>
    /// Aranan işaretin kartı sayfa yüksekliğine göre küçülüyor. Seçenek
    /// kutucukları kendi kaplarını paylaşıyor (<c>BoardView</c>), yani onlar
    /// için burada bir hesap yok.
    /// </summary>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);
        _viewModel.OnPageHeightChanged(height);
    }

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
