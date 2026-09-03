using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class AdditionPage : ContentPage
{
    private readonly AdditionViewModel _viewModel;

    public AdditionPage(AdditionViewModel viewModel)
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
