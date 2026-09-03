using Ploofy.App.ViewModels;
using Ploofy.Engine.Games;
using Ploofy.Ui.Controls;

namespace Ploofy.App.Views;

public partial class ColoringPage : ContentPage
{
    private readonly ColoringViewModel _viewModel;

    public ColoringPage(ColoringViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.RoundReady += OnRoundReady;
        Surface.Painted += OnSurfacePainted;
        Surface.RoundOver += OnRoundOver;
    }

    private void OnRoundReady(object? sender, ColoringRound round) => Surface.Start(round);

    private void OnSurfacePainted(object? sender, PaintEventArgs e) =>
        _viewModel.OnPainted(e.Outcome);

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
