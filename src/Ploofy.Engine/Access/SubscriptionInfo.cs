namespace Ploofy.Engine.Access;

/// <summary>
/// Aboneliğin ebeveyne gösterilecek hâli: durum + ödenmiş dönemin sonu.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Entitlements"/> "neye erişilebilir"i söylüyor; burası "ne zamana
/// kadar"ı. İkisi ayrı duruyor çünkü erişim kararı hiçbir tarihe bakmıyor —
/// tarihin tek işi ebeveyn ekranındaki cümleyi kurmak.
/// </para>
/// <para>
/// Tarih mağazadan geliyor ve <c>null</c> olabilir: ücretsiz katmanda dönem
/// yok, çevrimdışı ilk açılışta da mağazaya sorulamamış olabilir. Bu yüzden
/// hiçbir kural tarihin varlığına bağlanmadı.
/// </para>
/// </remarks>
public sealed record SubscriptionInfo(SubscriptionStatus Status, DateOnly? PeriodEndsOn = null)
{
    public static SubscriptionInfo Free { get; } = new(SubscriptionStatus.None);

    public Entitlements Entitlements => new(Status);

    /// <summary>Dönem sonunda yenilenecekse yenileme tarihi.</summary>
    public DateOnly? RenewsOn => Entitlements.AutoRenews ? PeriodEndsOn : null;

    /// <summary>Abonelik bitirildiyse erişimin kapanacağı tarih.</summary>
    public DateOnly? AccessEndsOn => Entitlements.AccessEndsAfterPeriod ? PeriodEndsOn : null;

    /// <summary>
    /// Ödenmiş dönemin dolup dolmadığı.
    /// </summary>
    /// <remarks>
    /// Uygulama bunu kendi başına "artık abone değil"e çevirmiyor — o kararı
    /// mağaza veriyor. Burası yalnızca ekranın "süresi geçmiş" diyebilmesi
    /// için var; cihazın saati geri alınarak erişim uzatılamaz, çünkü erişim
    /// zaten tarihe değil duruma bakıyor.
    /// </remarks>
    public bool HasExpired(DateOnly today) =>
        PeriodEndsOn is { } end && end < today;

    /// <summary>Dönem sonuna kalan tam gün sayısı; dönem yoksa <c>null</c>.</summary>
    public int? DaysLeft(DateOnly today) =>
        PeriodEndsOn is { } end ? Math.Max(0, end.DayNumber - today.DayNumber) : null;

    /// <summary>
    /// Aboneliğin bugün gerçekte geldiği hâl.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Bitirilmiş bir aboneliğin ödenmiş dönemi dolduğunda erişim <b>kapanır</b>
    /// ve kilitler geri gelir. Bunu söylemek için mağazaya sormaya gerek yok:
    /// iptal edildiğinde mağaza zaten "yenilemeyecek" demiş oluyor, yani o
    /// tarih kesin. Bu kural olmadan iptal edilen abonelik kâğıt üstünde
    /// bitiyor ama uygulamada sonsuza kadar açık kalıyordu.
    /// </para>
    /// <para>
    /// <see cref="SubscriptionStatus.Active"/> ve
    /// <see cref="SubscriptionStatus.Grace"/> <b>düşürülmüyor</b>. Onlarda tarih
    /// yalnızca son bilinen yenileme günü: mağaza yeniledikçe ileri kayıyor ve
    /// uygulama çevrimdışıyken sorulamıyor. Süresi geçmiş diye kapatmak,
    /// uçaktaki bir aileyi parasını ödedikleri oyunlardan etmek olurdu.
    /// </para>
    /// <para>
    /// Tarih yoksa da düşürülmüyor: bilinmeyen bir tarih, bitmiş bir dönem
    /// değil. Eksik veriye dayanarak erişim kapatmak yanlış yönde bir hata.
    /// </para>
    /// </remarks>
    public SubscriptionInfo AsOf(DateOnly today) =>
        Status == SubscriptionStatus.Canceled && HasExpired(today) ? Free : this;
}
