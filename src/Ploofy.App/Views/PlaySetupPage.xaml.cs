using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class PlaySetupPage : ContentPage
{
    private readonly PlaySetupViewModel _viewModel;

    public PlaySetupPage(PlaySetupViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
