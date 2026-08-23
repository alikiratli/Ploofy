using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class PaywallPage : ContentPage
{
    private readonly PaywallViewModel _viewModel;

    public PaywallPage(PaywallViewModel viewModel)
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
