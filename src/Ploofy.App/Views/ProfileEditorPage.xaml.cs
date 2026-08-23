using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class ProfileEditorPage : ContentPage
{
    public ProfileEditorPage(ProfileEditorViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = viewModel;
    }
}
