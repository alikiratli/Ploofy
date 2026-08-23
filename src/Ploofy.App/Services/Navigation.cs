namespace Ploofy.App.Services;

/// <summary>
/// Birden çok ekranın ihtiyaç duyduğu gezinme kısayolları.
/// </summary>
public static class Navigation
{
    /// <summary>
    /// Oyun akışından çıkıp oyun listesine döner.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Tek bir mutlak çağrı: oyun listesi kabuğun kendi kökü
    /// (bkz. AppShell.xaml), bu yüzden kaç ekran derinde olduğumuzun önemi
    /// yok ve yığın kendiliğinden sıfırlanıyor.
    /// </para>
    /// <para>
    /// Burası bir zamanlar önce <c>//profiles</c>'a gidip sonra oyun
    /// listesini itiyordu. Ardışık iki gezinme çağrısı, ebeveyn kilidi
    /// diyaloğu kapanırken tetiklendiğinde kabuğu kilitliyordu: uygulama
    /// ayakta kalıyor ama hiçbir dokunuşa cevap vermiyordu.
    /// </para>
    /// </remarks>
    public static Task GoHomeAsync() => Shell.Current.GoToAsync("//home");
}
