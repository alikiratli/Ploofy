using Ploofy.App.ViewModels;
using Ploofy.Engine.Games;
using Ploofy.Ui.Controls;

namespace Ploofy.App.Views;

public partial class DotToDotPage : ContentPage
{
    private readonly DotToDotViewModel _viewModel;

    public DotToDotPage(DotToDotViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.RoundReady += OnRoundReady;
        Surface.Tapped += OnSurfaceTapped;
        Surface.RoundOver += OnRoundOver;
    }

    private void OnRoundReady(object? sender, DotToDotRound round) => Surface.Start(round);

    private void OnSurfaceTapped(object? sender, DotTapEventArgs e) =>
        _viewModel.OnTapped(e.Result);

    private async void OnRoundOver(object? sender, EventArgs e)
    {
        // Yüzey biten resmi zaten kutlama süresince gösterdi; buradaki kısa
        // pay son parçacıkların sönmesi için.
        await Task.Delay(300);
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
