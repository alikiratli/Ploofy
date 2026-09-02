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
}
