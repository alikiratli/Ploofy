using Ploofy.Engine.Progress;

namespace Ploofy.App.Services;

/// <summary>Aynı temadaki avatarlar — ekranda başlığıyla birlikte duruyor.</summary>
/// <param name="NameKey">Grup başlığının kaynak anahtarı.</param>
public sealed record AvatarGroup(string NameKey, IReadOnlyList<string> Avatars);

/// <summary>
/// Çocuğun kendine seçtiği simge.
/// </summary>
/// <remarks>
/// <para>
/// Görsel dosya değil emoji. Sebepleri: üç dilde de aynı, ek varlık
/// gerektirmiyor, her platformda renkli görünüyor ve uygulama boyutunu
/// büyütmüyor. Çizilmiş karakterler daha "bize ait" olurdu ama otuz iki
/// karakteri çizdirmek, üç dilde adlandırmak ve her ekran yoğunluğu için
/// ölçeklemek demek — bunun karşılığı henüz yok.
/// </para>
/// <para>
/// Lisanslı çizgi film karakteri <b>yok</b> ve olamaz: mağazaya çıkacak bir
/// çocuk uygulamasında bu doğrudan hak ihlali. Masal grubundaki karakterler
/// (tek boynuzlu at, peri, ejderha, robot) kimseye ait değil ve çocuğun
/// aradığı "kahraman" hissini karşılıyor.
/// </para>
/// <para>
/// Sıralama ve içerik <b>değiştirilebilir ama silinemez</b>: kayıtlı
/// profiller avatarını metin olarak tutuyor, listeden çıkan bir emoji o
/// çocuğun profilinde boş kutu olarak görünür.
/// </para>
/// <para>
/// Avatarların bir kısmı yıldızla açılıyor — bkz. <see cref="UnlockOrder"/>.
/// </para>
/// </remarks>
public static class AvatarCatalog
{
    public static readonly IReadOnlyList<AvatarGroup> Groups =
    [
        new("AvatarGroupAnimals",
            ["🦊", "🐻", "🐼", "🐨", "🦁", "🐯", "🐰", "🐸", "🐷", "🐮", "🐵", "🦉"]),

        new("AvatarGroupSea",
            ["🐧", "🐢", "🐙", "🐠", "🐳", "🦀", "🦋", "🐝", "🐞", "🦆"]),

        // Masal kahramanları: çocuğun "ben bu olayım" dediği grup. Hepsi
        // sahipsiz karakterler — bkz. sınıf açıklaması.
        //
        // Altıncı sırada bir zamanlar süper kahraman vardı; Unicode 11 ile
        // geldiği için ancak Android 9.0'da çiziliyor, uygulamanın alt sınırı
        // ise 8.0. Bir avatarın boş kutu çıkması ödülün kendisini bozuyor,
        // o yüzden yerini Unicode 9 olan T-Rex aldı. Değişiklik yayından
        // önce yapıldı: sonrasında olsaydı süper kahramanı seçmiş bir
        // çocuğun profili bozulurdu (bkz. sınıf açıklaması).
        new("AvatarGroupHeroes",
            ["🦄", "🐉", "🧚", "🧙", "🧜", "🦖", "🤖", "👽", "👻", "🦕"]),
    ];

    /// <summary>Bütün avatarlar, gruplardaki sırayla.</summary>
    public static readonly IReadOnlyList<string> All =
        [.. Groups.SelectMany(g => g.Avatars)];

    /// <summary>
    /// Başlangıçtan açık gelen avatarlar: hayvanlar grubunun tamamı.
    /// </summary>
    /// <remarks>
    /// On iki seçenek, hiç oynamamış bir çocuğun kendine benzeteceği birini
    /// bulması için fazlasıyla yeterli. Açılışta yalnızca üç dört avatar
    /// bırakmak kilidi ödül değil engel yapardı: profil kurma ekranı,
    /// uygulamanın çocuğa gösterdiği ilk ekran.
    /// </remarks>
    public static readonly IReadOnlyList<string> Free =
        [.. Groups[0].Avatars];

    /// <summary>
    /// Kilitli avatarların açılma sırası.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Deniz ve masal grupları <b>dönüşümlü</b> diziliyor, grup grup değil.
    /// Grup grup olsaydı masal kahramanları — çocuğun asıl istediği grup —
    /// en sona düşerdi ve ilk otuz yıldız boyunca ödül olarak hep bir deniz
    /// hayvanı gelirdi.
    /// </para>
    /// <para>
    /// Sıra tek boynuzlu atla başlıyor: ilk ödül üç yıldızda, yani ilk tam
    /// turun sonunda geliyor ve kütüphanenin en çekici simgesi oluyor. İlk
    /// ödül zayıf olursa kuralın kendisi fark edilmiyor.
    /// </para>
    /// <para>
    /// <b>Sıra değişebilir ama listeden avatar çıkarılamaz.</b> Çıkarılan bir
    /// avatar, onu seçmiş çocuğun profilinde kilitli görünür. Yeni avatarlar
    /// sona eklenir; araya sokmak, çocuğun daha önce açtığı bir simgeyi geri
    /// kilitler.
    /// </para>
    /// </remarks>
    public static readonly IReadOnlyList<string> UnlockOrder = BuildUnlockOrder();

    /// <summary>Emoji -> istenen yıldız. Avatar ızgarası her çizimde soruyor.</summary>
    private static readonly Dictionary<string, int> Thresholds = UnlockOrder
        .Select((emoji, index) => (emoji, stars: RewardLadder.RequiredStars(index + 1)))
        .ToDictionary(pair => pair.emoji, pair => pair.stars, StringComparer.Ordinal);

    /// <summary>Yeni profilin açılışta seçili gelen avatarı.</summary>
    public static string Default => All[0];

    /// <summary>Yıldızla açılan avatar sayısı.</summary>
    public static int LockableCount => UnlockOrder.Count;

    /// <summary>
    /// Avatarın istediği toplam yıldız; başlangıçtan açıksa sıfır.
    /// </summary>
    /// <remarks>
    /// Katalogda hiç bulunmayan bir emoji de sıfır dönüyor: tanınmayan bir
    /// simgeyi kilitli saymak, eski bir profilin avatarını elinden almak olurdu.
    /// </remarks>
    public static int RequiredStars(string emoji) =>
        Thresholds.TryGetValue(emoji, out var stars) ? stars : 0;

    /// <summary>Avatar bu yıldız sayısıyla açık mı?</summary>
    public static bool IsUnlocked(string emoji, int totalStars) =>
        totalStars >= RequiredStars(emoji);

    /// <summary>Merdivenin o andaki durumu.</summary>
    public static RewardProgress Progress(int totalStars) =>
        RewardLadder.Evaluate(totalStars, LockableCount);

    /// <summary>
    /// Yıldız <paramref name="before"/>'dan <paramref name="after"/>'a
    /// çıkarken açılan avatarlar, açılma sırasıyla.
    /// </summary>
    /// <remarks>
    /// Tur sonu ekranı bunu kutluyor. Liste dönüyor çünkü tek bir tur üç
    /// yıldız birden getirebiliyor ve bir eşiği tam ortasından atlayabiliyor;
    /// yalnızca sonuncuyu göstermek arada kalanı sessizce yutardı.
    /// </remarks>
    public static IReadOnlyList<string> UnlockedBetween(int before, int after)
    {
        var from = RewardLadder.UnlockedCount(before, LockableCount);
        var to = RewardLadder.UnlockedCount(after, LockableCount);

        return to <= from ? [] : [.. UnlockOrder.Skip(from).Take(to - from)];
    }

    private static IReadOnlyList<string> BuildUnlockOrder()
    {
        var heroes = Groups[2].Avatars;
        var sea = Groups[1].Avatars;

        var order = new List<string>(heroes.Count + sea.Count);
        for (var i = 0; i < Math.Max(heroes.Count, sea.Count); i++)
        {
            if (i < heroes.Count)
            {
                order.Add(heroes[i]);
            }

            if (i < sea.Count)
            {
                order.Add(sea[i]);
            }
        }

        return order;
    }
}
