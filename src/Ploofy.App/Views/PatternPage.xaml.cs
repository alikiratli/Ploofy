using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class PatternPage : ContentPage
{
    private readonly PatternViewModel _viewModel;

    public PatternPage(PatternViewModel viewModel)
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
