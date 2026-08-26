using Ploofy.App.ViewModels;
using Ploofy.Engine.Games;
using Ploofy.Ui.Controls;

namespace Ploofy.App.Views;

public partial class JigsawPage : ContentPage
{
    private readonly JigsawViewModel _viewModel;

    public JigsawPage(JigsawViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        _viewModel.RoundReady += OnRoundReady;
        Surface.Dropped += OnSurfaceDropped;
        Surface.RoundOver += OnRoundOver;
    }

    private void OnRoundReady(object? sender, JigsawRound round) => Surface.Start(round);

    private void OnSurfaceDropped(object? sender, JigsawDropEventArgs e) =>
        _viewModel.OnDropped(e.Outcome);

    private async void OnRoundOver(object? sender, EventArgs e)
    {
        // Tamamlanan resmin görülmesi için biraz uzun bir bekleme: yapbozda
        // ödül tablonun kendisi ve tur biter bitmez ekran değişirse çocuk
        // yaptığı resmi hiç göremiyor.
        await Task.Delay(1100);
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
