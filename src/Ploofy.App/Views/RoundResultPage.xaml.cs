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

    protected override void OnAppearing()
    {
        base.OnAppearing();
        _viewModel.Load();
    }
}
