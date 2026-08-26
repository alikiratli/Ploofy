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
        new("AvatarGroupHeroes",
            ["🦄", "🐉", "🧚", "🧙", "🧜", "🦸", "🤖", "👽", "👻", "🦕"]),
    ];

    /// <summary>Bütün avatarlar, gruplardaki sırayla.</summary>
    public static readonly IReadOnlyList<string> All =
        [.. Groups.SelectMany(g => g.Avatars)];

    /// <summary>Yeni profilin açılışta seçili gelen avatarı.</summary>
    public static string Default => All[0];
}
