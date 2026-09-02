using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class SubscriptionPage : ContentPage
{
    private readonly SubscriptionViewModel _viewModel;

    public SubscriptionPage(SubscriptionViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override void OnAppearing()
    {
        base.OnAppearing();

        // Paywall'dan dönüldüğünde durum değişmiş olabiliyor; her görünüşte
        // yeniden okunuyor.
        _viewModel.Load();
    }
}
