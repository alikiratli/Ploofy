using Android.App;
using Android.Content.PM;
using Android.OS;

namespace Ploofy.App;

/// <summary>
/// Uygulamanın tek Android etkinliği.
/// </summary>
/// <remarks>
/// <para>
/// Ekran <b>yatayda kilitli</b>. Bütün oyun yerleşimleri yatay tablet
/// düşünülerek ölçüldü: dikeyde tahta ile tepsi arasında yarım ekranlık
/// boşluklar kalıyor, sepet ve rakam tepsileri ekranın dar kenarına
/// sıkışıyor. İki yönü birden desteklemek her oyun için ikinci bir yerleşim
/// yazmak demekti; bu yaş grubunda kazancı yok, çünkü tablet zaten çoğu
/// zaman masaya yatay konuyor.
/// </para>
/// <para>
/// <c>SensorLandscape</c> seçildi, <c>Landscape</c> değil: çocuk tableti
/// ters çevirdiğinde görüntü de dönüyor, ekran baş aşağı kalmıyor.
/// </para>
/// </remarks>
[Activity(
    Theme = "@style/Maui.SplashTheme",
    MainLauncher = true,
    LaunchMode = LaunchMode.SingleTop,
    ScreenOrientation = ScreenOrientation.SensorLandscape,
    ConfigurationChanges = ConfigChanges.ScreenSize | ConfigChanges.Orientation | ConfigChanges.UiMode | ConfigChanges.ScreenLayout | ConfigChanges.SmallestScreenSize | ConfigChanges.Density)]
public class MainActivity : MauiAppCompatActivity
{
}
