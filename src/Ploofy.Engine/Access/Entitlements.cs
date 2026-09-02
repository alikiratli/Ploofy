using Ploofy.Engine.Catalog;

namespace Ploofy.Engine.Access;

/// <summary>
/// Aboneliğin cihazdaki durumu.
/// </summary>
/// <remarks>
/// Doğruluk kaynağı mağazadır (App Store / Play Billing). Uygulamanın kendi
/// hesabı, sunucusu ya da kullanıcı kaydı yok — çocuğa ait hiçbir veri
/// cihazdan çıkmadığı için COPPA / GDPR-K tarafında taşınacak yük de yok.
/// </remarks>
public enum SubscriptionStatus
{
    /// <summary>Hiç abone olunmamış ya da abonelik bitmiş.</summary>
    None,

    /// <summary>Abonelik geçerli.</summary>
    Active,

    /// <summary>
    /// Ödeme sorunu var ama mağaza hâlâ erişim tanıyor (billing grace period).
    /// Erişim açık kalır; ebeveyne yalnızca ayarlarda hatırlatma gösterilir —
    /// çocuğun oyununu ödeme uyarısıyla kesmek doğru değil.
    /// </summary>
    Grace,

    /// <summary>
    /// Abonelik bitirildi ama ödenmiş dönem henüz dolmadı.
    /// </summary>
    /// <remarks>
    /// Hem Play hem App Store aynı şekilde davranıyor: iptal, otomatik
    /// yenilemeyi kapatır; erişim dönem sonuna kadar sürer. Erişimi iptal
    /// anında kesmek ebeveynin parasını yakmak olurdu ve mağazanın kendi
    /// davranışıyla da çelişirdi.
    /// </remarks>
    Canceled,
}

/// <summary>
/// Kullanıcının neye erişebildiği.
/// </summary>
/// <remarks>
/// Tek karar noktası burasıdır: hiçbir ekran "abone mi?" diye kendi başına
/// karar vermez, <see cref="CanPlay"/> / <see cref="CanAddProfile"/> sorar.
/// Katman kuralları değişirse tek dosya değişir.
/// </remarks>
public sealed record Entitlements(SubscriptionStatus Status)
{
    /// <summary>Ücretsiz katmanda izin verilen çocuk profili sayısı.</summary>
    public const int FreeProfileLimit = 1;

    /// <summary>
    /// Abonelikte izin verilen profil sayısı. Sınır teknik değil, ürünsel:
    /// bir ailenin gerçekten ihtiyaç duyacağından fazlası paylaşılan hesap
    /// anlamına gelir.
    /// </summary>
    public const int SubscribedProfileLimit = 4;

    public static Entitlements Free { get; } = new(SubscriptionStatus.None);

    public static Entitlements Subscribed { get; } = new(SubscriptionStatus.Active);

    public bool HasFullAccess =>
        Status is SubscriptionStatus.Active or SubscriptionStatus.Grace
            or SubscriptionStatus.Canceled;

    /// <summary>Ödeme sorunu var mı? Yalnızca ebeveyn ekranında gösterilir.</summary>
    public bool NeedsBillingAttention => Status == SubscriptionStatus.Grace;

    /// <summary>Dönem sonunda kendiliğinden yenilenecek mi?</summary>
    public bool AutoRenews =>
        Status is SubscriptionStatus.Active or SubscriptionStatus.Grace;

    /// <summary>
    /// Erişim var ama dönem sonunda bitecek. Ebeveyn ekranı buna bakıp
    /// "şu tarihte kapanacak" diyor ve yeniden başlatmayı öneriyor.
    /// </summary>
    public bool AccessEndsAfterPeriod => Status == SubscriptionStatus.Canceled;

    /// <summary>Bitirilecek bir abonelik var mı?</summary>
    public bool CanCancel => Status is SubscriptionStatus.Active or SubscriptionStatus.Grace;

    public int ProfileLimit => HasFullAccess ? SubscribedProfileLimit : FreeProfileLimit;

    /// <summary>
    /// Reklam var mı? Hiçbir katmanda yok — ücretsiz katman da reklamsız.
    /// Sabit olarak duruyor ki ileride "ücretsiz katmana reklam koyalım mı"
    /// tartışması açıldığında tek bir yerde ve bilinçli olarak değişsin.
    /// </summary>
    public bool ShowsAds => false;

    public bool CanPlay(MiniGameDescriptor game) =>
        game.Tier == GameTier.Free || HasFullAccess;

    public bool CanAddProfile(int currentProfileCount) =>
        currentProfileCount < ProfileLimit;

    /// <summary>
    /// Çevrimdışı oynanabilir mi? Ücretsiz katman da çevrimdışı çalışır (oyunlar
    /// zaten cihazda); abonelikte ek olarak indirilebilir tema/içerik paketleri
    /// çevrimdışı kullanılabilir.
    /// </summary>
    public bool CanDownloadContentPacks => HasFullAccess;

    /// <summary>
    /// Sıralı oyun birden fazla profil gerektirdiği için pratikte aboneliğe
    /// bağlı. Yine de ücretsiz katmanda "misafir" oyuncuyla tek seferlik
    /// denenebilir — özelliği hiç göstermemek satın alma kararına yardımcı olmuyor.
    /// </summary>
    public bool CanUseMultipleProfilesInSession => HasFullAccess;

    /// <summary>Bu kullanıcı için katalogda kilitli görünen oyunlar.</summary>
    public IReadOnlyList<MiniGameDescriptor> LockedGames() =>
        GameCatalog.Games.Where(g => !CanPlay(g)).ToList();
}
