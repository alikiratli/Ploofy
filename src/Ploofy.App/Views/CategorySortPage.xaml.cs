using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class CategorySortPage : ContentPage
{
    private readonly CategorySortViewModel _viewModel;

    public CategorySortPage(CategorySortViewModel viewModel)
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
