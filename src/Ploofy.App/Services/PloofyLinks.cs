namespace Ploofy.App.Services;

/// <summary>
/// Uygulamadan dışarı çıkan adresler.
/// </summary>
/// <remarks>
/// <para>
/// Hepsi ebeveyn kilidinin arkasından açılıyor
/// (<c>ParentalGateReason.ExternalLink</c>): çocuğun tek dokunuşla tarayıcıya
/// düşmesi bu yaş grubunda kabul edilebilir değil ve mağaza kuralları da
/// bunu istiyor.
/// </para>
/// <para>
/// Sayfaların kaynağı depodaki <c>docs/store/</c>; yayın nüshası
/// <c>alikiratli/ploofy-web</c> deposunda ve GitHub Pages'ten servis
/// ediliyor. Adres değişirse tek yer burası — Play Console'daki alanın da
/// birlikte güncellenmesi gerekir.
/// </para>
/// </remarks>
public static class PloofyLinks
{
    public static Uri PrivacyPolicy { get; } =
        new("https://alikiratli.github.io/ploofy-web/privacy-policy.html");

    public static Uri Imprint { get; } =
        new("https://alikiratli.github.io/ploofy-web/impressum.html");
}
