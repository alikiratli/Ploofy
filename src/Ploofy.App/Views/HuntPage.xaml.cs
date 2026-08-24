using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class HuntPage : ContentPage
{
    private readonly HuntViewModel _viewModel;

    public HuntPage(HuntViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        _viewModel.Dispose();
    }
}
