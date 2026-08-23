using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class ProfilePickerPage : ContentPage
{
    private readonly ProfilePickerViewModel _viewModel;

    public ProfilePickerPage(ProfilePickerViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>
    /// Her görünüşte yeniden yükleniyor: profil eklendikten ya da silindikten
    /// sonra bu ekrana geri dönülüyor ve liste güncel olmalı.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }
}
