using Ploofy.App.ViewModels;

namespace Ploofy.App.Views;

public partial class HomePage : ContentPage
{
    private readonly HomeViewModel _viewModel;

    public HomePage(HomeViewModel viewModel)
    {
        InitializeComponent();
        BindingContext = _viewModel = viewModel;
    }

    /// <summary>
    /// Oyundan dönüldüğünde yıldızlar güncellenmiş oluyor; her görünüşte
    /// yeniden yükleniyor.
    /// </summary>
    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await _viewModel.LoadAsync();
    }

    /// <summary>
    /// Sütun sayısı genişliğe göre ayarlanıyor.
    /// </summary>
    /// <remarks>
    /// Eşik kutucuğun rahat okunduğu en küçük genişliğe göre: 900 birimin
    /// altında iki sütun (telefon ve dar tablet), üstünde üç. Sabit iki
    /// sütun yatay tablette kutucukları ekran boyu şeritlere çeviriyordu.
    /// </remarks>
    protected override void OnSizeAllocated(double width, double height)
    {
        base.OnSizeAllocated(width, height);

        if (width > 0)
        {
            _viewModel.GameColumns = width >= 900 ? 3 : 2;
        }
    }
}
