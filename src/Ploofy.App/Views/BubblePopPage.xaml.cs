using Ploofy.App.ViewModels;
using Ploofy.Engine.Games;
using Ploofy.Ui.Controls;

namespace Ploofy.App.Views;

public partial class BubblePopPage : ContentPage
{
    private readonly BubblePopViewModel _viewModel;

    public BubblePopPage(BubblePopViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;

        // Çizim yüzeyi ile görünüm modeli arasındaki köprü. Yüzey oyunu
        // bilmiyor, görünüm modeli çizimi bilmiyor; ikisini burası bağlıyor.
        _viewModel.RoundReady += OnRoundReady;
        Surface.Touched += OnSurfaceTouched;
        Surface.FrameRendered += OnSurfaceFrame;
        Surface.RoundOver += OnRoundOver;
    }

    private void OnRoundReady(object? sender, BubblePopRound round) => Surface.Start(round);

    private void OnSurfaceTouched(object? sender, BubbleTouchEventArgs e) =>
        _viewModel.OnTouched(e.Outcome);

    private void OnSurfaceFrame(object? sender, EventArgs e) => _viewModel.OnFrame();

    private async void OnRoundOver(object? sender, EventArgs e)
    {
        // Son patlamanın parçacıkları görünsün diye kısa bir bekleme; tur
        // biter bitmez ekran değişirse çocuk kazandığını göremiyor.
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
