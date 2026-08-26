using Ploofy.App.ViewModels;
using Ploofy.Engine.Games;
using Ploofy.Ui.Controls;

namespace Ploofy.App.Views;

public partial class BasketCatchPage : ContentPage
{
    private readonly BasketCatchViewModel _viewModel;

    public BasketCatchPage(BasketCatchViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.RoundReady += OnRoundReady;
        Surface.Catch += OnSurfaceCatch;
        Surface.RoundOver += OnRoundOver;
    }

    private void OnRoundReady(object? sender, BasketCatchRound round) => Surface.Start(round);

    private void OnSurfaceCatch(object? sender, BasketCatchEventArgs e) =>
        _viewModel.OnCatch(e.Caught);

    private async void OnRoundOver(object? sender, EventArgs e)
    {
        // Son yakalamanın parçacıkları görünsün diye kısa bir bekleme; tur
        // biter bitmez ekran değişirse çocuk bitirdiğini göremiyor.
        Surface.Pause();
        await Task.Delay(700);
        await _viewModel.CompleteRoundAsync();
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();

        Surface.Stop();
        _viewModel.Dispose();
    }
}
