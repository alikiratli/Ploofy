using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.App.Services;

/// <summary>
/// Harf Yazma'nın içerik havuzu.
/// </summary>
/// <remarks>
/// <para>
/// Motor havuzu dışarıdan istiyor çünkü alfabe dile göre değişiyor ve motorun
/// dil bilmesi gerekmiyor — Harf Avı'nda da düzen aynı (bkz.
/// <see cref="HuntContent"/>).
/// </para>
/// <para>
/// Havuz harf <b>ve</b> rakam taşıyor. İkisini ayrı oyunlara bölmedik: avda
/// bölme sebebi çocuğun "harf oyunu" ile "sayı oyunu"nu ayrı seçmek istemesi,
/// burada ise yazma hareketi ikisinde de aynı ve bir turda ikisinin karışması
/// kalem tutmayı çeşitlendiriyor.
/// </para>
/// </remarks>
public static class LetterTraceContent
{
    /// <summary>
    /// Yazılacak işaretler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Fidan bandı yalnızca <b>kolay</b> işaretler görüyor: düz çizgiden
    /// oluşanlar ve tek halkalılar. E'nin dört darbesi ya da S'nin çift
    /// kıvrımı bu yaşta yazmayı öğretmiyor, yalnızca yoruyor.
    /// </para>
    /// <para>
    /// Meşe bandı alfabenin tamamını görüyor. Aksanlı harfler de dahil:
    /// Türkçe'de Ç ve Ş, Almanca'da Ä ve Ö çocuğun kendi adını yazarken
    /// ihtiyaç duyduğu harfler, ve gövdeleri zaten C ile O'nunki.
    /// </para>
    /// <para>
    /// Havuzdaki bir işaretin yazım yolu yoksa motor onu sessizce eliyor —
    /// listeler burada elle tutulduğu için bu yalnızca bir emniyet kemeri.
    /// </para>
    /// </remarks>
    public static IReadOnlyList<string> PoolFor(string language, AgeBand band)
    {
        // Düz çizgiden oluşanlar ve tek halkalılar; küçük bir çocuğun ilk
        // yazdığı harfler.
        string[] easyLetters = ["A", "E", "H", "I", "L", "O", "T", "U", "V", "X", "Y"];
        string[] easyDigits = ["1", "4", "7", "0"];

        if (band == AgeBand.Fidan)
        {
            return [.. easyLetters, .. easyDigits];
        }

        string[] letters = language switch
        {
            "tr" =>
            [
                "A", "B", "C", "Ç", "D", "E", "F", "G", "Ğ", "H", "I", "İ", "J", "K", "L",
                "M", "N", "O", "Ö", "P", "R", "S", "Ş", "T", "U", "Ü", "V", "Y", "Z",
            ],
            "de" =>
            [
                "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
                "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
                "Ä", "Ö", "Ü",
            ],
            _ =>
            [
                "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
                "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
            ],
        };

        string[] digits = ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];

        return [.. letters, .. digits];
    }

    /// <summary>
    /// Üstteki şeritte duran yönerge anahtarı.
    /// </summary>
    /// <remarks>
    /// Harf ile rakam ayrı cümle istiyor: "harfi çiz" derken ekranda 7
    /// duruyorsa çocuk değil ebeveyn şaşırıyor.
    /// </remarks>
    public static string HintKey(Glyph glyph) =>
        char.IsDigit(glyph.Character[0]) ? "LetterTraceHintNumber" : "LetterTraceHintLetter";
}
