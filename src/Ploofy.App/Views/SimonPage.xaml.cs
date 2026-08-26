using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class SimonPage : ContentPage
{
    private readonly SimonViewModel _viewModel;

    public SimonPage(SimonViewModel viewModel)
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

        // Gösterim burada kesiliyor; yoksa kapanmış ekranda tuş yanmaya
        // devam ediyor ve bir sonraki açılışta oyun yarısından başlıyor.
        _viewModel.Dispose();
    }
}
