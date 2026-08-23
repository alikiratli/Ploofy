using Ploofy.App.ViewModels;
using Ploofy.Engine.Games;
using Ploofy.Ui.Controls;

namespace Ploofy.App.Views;

public partial class ShapeSortPage : ContentPage
{
    private readonly ShapeSortViewModel _viewModel;

    public ShapeSortPage(ShapeSortViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.RoundReady += OnRoundReady;
        Surface.Dropped += OnSurfaceDropped;
        Surface.RoundOver += OnRoundOver;
    }

    private void OnRoundReady(object? sender, ShapeSortRound round) => Surface.Start(round);

    private void OnSurfaceDropped(object? sender, ShapeDropEventArgs e) =>
        _viewModel.OnDropped(e.Outcome);

    private async void OnRoundOver(object? sender, EventArgs e)
    {
        // Son parçanın kutuya yerleşmesi ve parçacıkları görünsün diye kısa
        // bir bekleme; tur biter bitmez ekran değişirse çocuk bitirdiğini
        // göremiyor.
        await Task.Delay(600);
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
