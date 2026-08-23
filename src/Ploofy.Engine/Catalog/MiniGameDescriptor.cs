namespace Ploofy.Engine.Catalog;

/// <summary>
/// Bir mini oyunun temel etkileşim türü.
/// </summary>
/// <remarks>
/// Katalogda bunu takip etmenin tek sebebi var: "hepsi aynı hissettiriyor"
/// sorununu önlemek. Yeni oyun eklenirken kütüphanedeki tür dağılımına bakılır;
/// beşinci dokunma oyunu yerine eksik olan türden bir oyun seçilir.
/// </remarks>
public enum InteractionKind
{
    /// <summary>Ekrandaki hedefe dokunma.</summary>
    Tap,

    /// <summary>Objeyi tutup bir hedefe bırakma.</summary>
    Drag,

    /// <summary>Parmağı ekrandan kaldırmadan bir yolu takip etme.</summary>
    Trace,

    /// <summary>Gördüğünü akılda tutup sonra bulma.</summary>
    Memory,

    /// <summary>Verilen sırayı / ritmi tekrarlama.</summary>
    Sequence,
}

public enum GameTier
{
    /// <summary>
    /// Ücretsiz katmanda tamamen açık — reklamsız, kesintisiz, kilitsiz.
    /// İndirme kararını kolaylaştıran vitrin oyunları.
    /// </summary>
    Free,

    /// <summary>Abonelikle açılır.</summary>
    Subscription,
}

/// <summary>
/// Hangi çizim tekniğiyle gerçeklendiği.
/// </summary>
/// <remarks>
/// Motorun bunu bilmesi gerekmez ama katalog tek envanter olduğu için burada
/// duruyor: yeni oyun planlarken hangi tarafın yükleneceğini görmek kolay olsun.
/// </remarks>
public enum RenderKind
{
    /// <summary>MAUI kontrolleri ve yerleşim animasyonları yeterli.</summary>
    Layout,

    /// <summary>Sürekli hareket / parçacık / serbest çizim gerektiriyor — SkiaSharp.</summary>
    Canvas,
}

/// <summary>
/// Bir mini oyunun motor tarafındaki tanımı.
/// </summary>
/// <remarks>
/// Burada görsel, sayfa ya da metin yok — yalnızca oyunun kim olduğu. Arayüz
/// katmanı <see cref="Id"/> üzerinden kendi sayfasını ve çevirisini eşler;
/// böylece motor MAUI'ye bağımlı olmadan kütüphaneyi tanıyabilir (kilit
/// durumu, bant uygunluğu, tür dağılımı).
/// </remarks>
/// <param name="Id">
/// Sabit anahtar. Veritabanındaki yıldız kayıtları buna bağlı — asla değişmez.
/// </param>
/// <param name="MinBand">
/// Oyunun anlamlı olduğu en küçük bant. Bunun altındaki bantta katalogda
/// gösterilmez (ör. harf avı 2 yaşındaki bir çocuk için anlamsız).
/// </param>
/// <param name="IsEducational">
/// Erken okuma / matematik hazırlığına dönük oyunlar. Ana ekranda ve ebeveyn
/// ekranında ayrı bölümde listelenir.
/// </param>
/// <param name="SupportsPassAndPlay">
/// Aynı cihazda sırayla oynamaya uygun mu? Serbest keşif modları (balon
/// patlatma gibi) sıraya bölünemediği için false.
/// </param>
public sealed record MiniGameDescriptor(
    string Id,
    InteractionKind Interaction,
    GameTier Tier,
    AgeBand MinBand,
    RenderKind Render,
    bool IsEducational = false,
    bool SupportsPassAndPlay = false);
