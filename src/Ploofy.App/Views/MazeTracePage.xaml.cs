using Ploofy.App.ViewModels;
using Ploofy.Engine.Games;
using Ploofy.Ui.Controls;

namespace Ploofy.App.Views;

public partial class MazeTracePage : ContentPage
{
    private readonly MazeTraceViewModel _viewModel;

    public MazeTracePage(MazeTraceViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.RoundReady += OnRoundReady;
        Surface.Traced += OnSurfaceTraced;
        Surface.RoundOver += OnRoundOver;
    }

    private void OnRoundReady(object? sender, MazeTraceRound round) => Surface.Start(round);

    private void OnSurfaceTraced(object? sender, TraceEventArgs e) =>
        _viewModel.OnTraced(e.Outcome);

    private async void OnRoundOver(object? sender, EventArgs e)
    {
        // Yüzey biten yolu zaten kutlama süresince gösterdi; buradaki kısa
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
