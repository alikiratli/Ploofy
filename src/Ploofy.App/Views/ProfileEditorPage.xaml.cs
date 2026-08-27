using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class ProfileEditorPage : ContentPage
{
    private readonly ProfileEditorViewModel _viewModel;

    public ProfileEditorPage(ProfileEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>
    /// Düzenleme parametresi kabuk tarafından sayfa görünmeden önce
    /// veriliyor; okuma burada, görünüm modelinin kurucusunda değil.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
