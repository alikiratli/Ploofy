using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class ReportPage : ContentPage
{
    private readonly ReportViewModel _viewModel;

    public ReportPage(ReportViewModel viewModel)
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
