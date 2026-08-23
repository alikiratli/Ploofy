namespace Ploofy.App.Services;

/// <summary>
/// Birden çok ekranın ihtiyaç duyduğu gezinme kısayolları.
/// </summary>
public static class Navigation
{
    /// <summary>
    /// Oyun akışından çıkıp ana ekrana (oyun listesine) döner.
    /// </summary>
    /// <remarks>
    /// Yığın oyun akışında derinleşiyor (profil → ana ekran → kurulum → oyun →
    /// sonuç); kaç adım geri gidileceğini saymak, araya bir ekran girdiği anda
    /// bozulacak bir kural olurdu. Bunun yerine kök sıfırlanıp ana ekran
    /// yeniden açılıyor: seçili profil <see cref="AppState"/> içinde durduğu
    /// için çocuk kendini yine kendi oyun listesinde buluyor.
    /// </remarks>
    public static async Task GoHomeAsync()
    {
        await Shell.Current.GoToAsync("//profiles");
        await Shell.Current.GoToAsync("home");
    }
}
