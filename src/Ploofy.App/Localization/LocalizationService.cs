using System.ComponentModel;
using System.Globalization;
using System.Resources;

namespace Ploofy.App.Localization;

/// <summary>
/// Uygulama metinlerinin tek kaynağı ve çalışırken dil değiştirme noktası.
/// </summary>
/// <remarks>
/// <para>
/// Metinler <c>Resources/Strings/AppStrings*.resx</c> dosyalarından okunuyor;
/// nötr dosya İngilizce, <c>.tr</c> ve <c>.de</c> uydu dosyaları.
/// Kaynak <c>content/strings.tsv</c>, üretici <c>tools/build_strings.py</c> —
/// resx dosyaları elle düzenlenmiyor.
/// </para>
/// <para>
/// Üretilen (designer) sınıf kullanılmıyor: dil ayarlardan değiştirildiğinde
/// açık duran ekranın anında güncellenmesi gerekiyor, bu da statik bir sınıf
/// yerine değişikliği duyuran bir gösterge istiyor. XAML tarafında
/// <c>{loc:Translate HomeTitle}</c> bu göstergeye bağlanıyor.
/// </para>
/// </remarks>
public sealed class LocalizationService : INotifyPropertyChanged
{
    /// <summary>Ayarlarda sunulan diller. Sıra ekranda göründüğü sıradır.</summary>
    public static readonly IReadOnlyList<string> SupportedLanguages = ["tr", "en", "de"];

    private static readonly ResourceManager Resources = new(
        "Ploofy.App.Resources.Strings.AppStrings",
        typeof(LocalizationService).Assembly);

    /// <summary>XAML işaretleme uzantısının bağlandığı örnek.</summary>
    public static LocalizationService Instance { get; } = new();

    private CultureInfo _culture = CultureInfo.CurrentUICulture;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>Anahtara karşılık gelen metin. Anahtar bulunamazsa anahtarın kendisi döner.</summary>
    public string this[string key] => Resources.GetString(key, _culture) ?? key;

    public string CurrentLanguage => _culture.TwoLetterISOLanguageName;

    /// <summary>
    /// Seçili dilin kültürü. Tarih biçimlendiren yerler bunu kullanıyor:
    /// <c>CultureInfo.CurrentCulture</c>'a güvenmek, dil değiştirildikten
    /// sonra oluşturulmuş bir iş parçacığında yanlış biçimi veriyor.
    /// </summary>
    public CultureInfo Culture => _culture;

    /// <summary>
    /// Dili değiştirir ve açık ekranların metinlerini tazeler.
    /// </summary>
    /// <param name="language">
    /// İki harfli kod. Desteklenmeyen bir kod gelirse İngilizceye düşülür —
    /// ayar bozuksa uygulama açılmamazlık etmemeli.
    /// </param>
    public void SetLanguage(string? language)
    {
        var code = SupportedLanguages.Contains(language) ? language! : "en";
        if (code == CurrentLanguage)
        {
            return;
        }

        _culture = new CultureInfo(code);

        // Tarih ve sayı biçimleri de aynı dile geçsin.
        CultureInfo.DefaultThreadCurrentCulture = _culture;
        CultureInfo.DefaultThreadCurrentUICulture = _culture;

        // "Item[]" bütün gösterge bağlarını tazeleyen özel ad.
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(CurrentLanguage)));
    }

    /// <summary>
    /// Kaydedilmiş bir dil yoksa cihaz dilini kullanır; cihaz dili
    /// desteklenmiyorsa İngilizce.
    /// </summary>
    public void ApplySavedOrDeviceLanguage(string? saved)
    {
        var device = CultureInfo.CurrentUICulture.TwoLetterISOLanguageName;
        SetLanguage(saved ?? (SupportedLanguages.Contains(device) ? device : "en"));
    }

    /// <summary>Biçimlendirilmiş metin — <c>{0}</c> yer tutucuları için.</summary>
    /// <remarks>
    /// <para>
    /// Tek argüman bir sayıysa ve değeri 1 ise önce anahtarın <c>.One</c>
    /// ekli tekil satırı aranıyor. İngilizce ve Almanca aksi hâlde
    /// "1 stars in total" / "Insgesamt 1 Sterne" yazıyordu.
    /// </para>
    /// <para>
    /// Tam bir çoğul kuralı motoru değil — bilerek. Desteklenen üç dilin
    /// üçünde de yalnızca "bir" ayrı davranıyor; Lehçe ya da Arapça gibi
    /// birden çok çoğul biçimi olan bir dil eklenirse burası
    /// <c>PluralRules</c>'a bakan bir seçime dönüşür.
    /// </para>
    /// </remarks>
    public string Format(string key, params object[] args)
    {
        if (args is [int and 1] && Resources.GetString($"{key}.One", _culture) is { } singular)
        {
            return string.Format(_culture, singular, args);
        }

        return string.Format(_culture, this[key], args);
    }
}
