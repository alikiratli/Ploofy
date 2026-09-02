using Ploofy.App.ViewModels;
using Ploofy.Engine.Games;
using Ploofy.Ui.Controls;

namespace Ploofy.App.Views;

public partial class LineUpPage : ContentPage
{
    private readonly LineUpViewModel _viewModel;

    public LineUpPage(LineUpViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.RoundReady += OnRoundReady;
        Surface.Placed += OnSurfacePlaced;
        Surface.PuzzleSolved += OnPuzzleSolved;
        Surface.RoundOver += OnRoundOver;
    }

    private void OnRoundReady(object? sender, LineUpRound round) => Surface.Start(round);

    private void OnSurfacePlaced(object? sender, LineUpPlaceEventArgs e) =>
        _viewModel.OnPlaced(e.Outcome);

    private void OnPuzzleSolved(object? sender, EventArgs e) => _viewModel.OnPuzzleSolved();

    private async void OnRoundOver(object? sender, EventArgs e)
    {
        // Yüzey tamamlanmış diziyi zaten kutlama süresince gösterdi; buradaki
        // kısa pay son parçacıkların sönmesi için.
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
