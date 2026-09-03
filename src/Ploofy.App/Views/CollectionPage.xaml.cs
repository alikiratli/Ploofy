using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class CollectionPage : ContentPage
{
    private readonly CollectionViewModel _viewModel;

    public CollectionPage(CollectionViewModel viewModel)
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
