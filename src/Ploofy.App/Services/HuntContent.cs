using System.Globalization;
using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.App.Services;

/// <summary>
/// Harf ve Sayı Avı'nın içerik havuzları.
/// </summary>
/// <remarks>
/// Motor havuzu dışarıdan istiyor çünkü alfabe dile göre değişiyor ve motorun
/// dil bilmesi gerekmiyor. Havuz aynı zamanda banda göre de değişiyor —
/// aşağıdaki iki karar oyunun öğretici tarafının tamamı.
/// </remarks>
public static class HuntContent
{
    // Türkçe alfabe (29 harf). Q, W, X yok; Ç, Ğ, İ, Ö, Ş, Ü var.
    private static readonly string[] TurkishUpper =
    [
        "A", "B", "C", "Ç", "D", "E", "F", "G", "Ğ", "H", "I", "İ", "J", "K", "L",
        "M", "N", "O", "Ö", "P", "R", "S", "Ş", "T", "U", "Ü", "V", "Y", "Z",
    ];

    private static readonly string[] EnglishUpper =
    [
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
    ];

    private static readonly string[] GermanExtras = ["Ä", "Ö", "Ü", "ß"];

    /// <summary>
    /// Aranabilecek harfler.
    /// </summary>
    /// <remarks>
    /// Meşe bandına küçük harfler de giriyor. Sebebi doğrudan pedagojik:
    /// b/d ve p/q ayrımı yalnızca küçük harflerde var ve bu ayrım okumaya
    /// geçişin gerçek eşiği. Fidan'da yalnızca büyük harf — o bantta amaç
    /// tanıma, ayırt etme değil.
    /// </remarks>
    public static IReadOnlyList<string> Letters(string language, AgeBand band)
    {
        var upper = language switch
        {
            "tr" => TurkishUpper,
            "de" => [.. EnglishUpper, .. GermanExtras],
            _ => EnglishUpper,
        };

        if (band != AgeBand.Mese)
        {
            return upper;
        }

        var culture = CultureFor(language);
        var lower = upper.Select(letter => letter.ToLower(culture)).ToArray();

        return [.. upper, .. lower];
    }

    /// <summary>
    /// Aranabilecek sayılar.
    /// </summary>
    /// <remarks>
    /// Filiz 1-5, Fidan 1-10, Meşe 0-25. Meşe'nin üst sınırı 20 değil 25:
    /// rakam sırasının ters okunması (12 ↔ 21) bu yaşta gerçek bir hata
    /// kaynağı ve çiftin ikisi de havuzda olmadan oyun bunu hiç
    /// gösteremiyor. Sıfırın girmesi de bilinçli — 0/8/9 ayrımı için.
    /// </remarks>
    public static IReadOnlyList<string> Numbers(AgeBand band)
    {
        var (from, to) = band switch
        {
            AgeBand.Filiz => (1, 5),
            AgeBand.Fidan => (1, 10),
            _ => (0, 25),
        };

        return Enumerable
            .Range(from, to - from + 1)
            .Select(n => n.ToString(CultureInfo.InvariantCulture))
            .ToList();
    }

    /// <summary>Oyun kimliğinden av türünü çözer.</summary>
    public static HuntKind KindFor(string gameId) =>
        gameId == Engine.Catalog.GameCatalog.NumberHunt ? HuntKind.Number : HuntKind.Letter;

    /// <summary>Seçili dile ve banda uygun havuz.</summary>
    public static IReadOnlyList<string> PoolFor(string gameId, string language, AgeBand band) =>
        KindFor(gameId) == HuntKind.Number ? Numbers(band) : Letters(language, band);

    private static CultureInfo CultureFor(string language)
    {
        try
        {
            return CultureInfo.GetCultureInfo(language);
        }
        catch (CultureNotFoundException)
        {
            return CultureInfo.InvariantCulture;
        }
    }
}
