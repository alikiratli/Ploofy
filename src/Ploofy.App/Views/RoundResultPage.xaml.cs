using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class RoundResultPage : ContentPage
{
    private readonly RoundResultViewModel _viewModel;

    public RoundResultPage(RoundResultViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Load();

        await CelebrateAsync();
    }

    /// <summary>
    /// Kutlama: kupa yaylanarak büyüyor, yıldızlar arkasından zıplıyor,
    /// konfeti dökülüyor.
    /// </summary>
    /// <remarks>
    /// Sıralama önemli. Hepsi aynı anda olursa ekran karışıyor; kupa → yıldız →
    /// konfeti sırası çocuğun gözünü önce ödüle, sonra kaç yıldız kazandığına
    /// götürüyor.
    /// </remarks>
    private async Task CelebrateAsync()
    {
        Trophy.Scale = 0.2;
        Trophy.Opacity = 0;
        SoloStars.Scale = 0.6;

        try
        {
            await Task.WhenAll(
                Trophy.FadeTo(1, 180),
                Trophy.ScaleTo(1.0, 420, Easing.SpringOut));

            await SoloStars.ScaleTo(1.0, 320, Easing.SpringOut);
        }
        catch (TaskCanceledException)
        {
            // Çocuk kutlamayı beklemeden çıktı; sayfa zaten kapanıyor.
            return;
        }

        Confetti.Celebrate();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        Confetti.Stop();
    }
}
