using System.Globalization;
using Ploofy.Data;
using Ploofy.Engine.Access;

namespace Ploofy.App.Services;

/// <summary>
/// Aboneliğin durumunu bildiren, satın almayı başlatan ve bitirmeyi mağazaya
/// devreden katman.
/// </summary>
/// <remarks>
/// Uygulamanın geri kalanı yalnızca bu arayüzü tanıyor. Mağaza bağlantısı
/// (Play Billing / StoreKit) buranın arkasına takılacak; ekranlar ve
/// <see cref="Entitlements"/> mantığı o gün değişmeyecek.
/// </remarks>
public interface ISubscriptionService
{
    /// <summary>Durum + ödenmiş dönemin sonu.</summary>
    SubscriptionInfo Info { get; }

    /// <summary>Erişim kararı. Ekranlar tarihe değil buna bakar.</summary>
    Entitlements Current => Info.Entitlements;

    event EventHandler<SubscriptionInfo>? Changed;

    /// <summary>Açılışta çağrılır: son bilinen durumu yükler.</summary>
    Task InitializeAsync();

    /// <summary>Satın alma akışını başlatır. Ebeveyn kilidinin arkasından çağrılmalı.</summary>
    Task<bool> PurchaseAsync();

    /// <summary>Mağazadaki mevcut satın alımları geri yükler.</summary>
    Task<bool> RestoreAsync();

    /// <summary>
    /// Aboneliği bitirir: otomatik yenileme kapanır, erişim ödenmiş dönemin
    /// sonuna kadar sürer.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Mağaza bağlandığında bu çağrı iptali <b>kendisi yapmayacak</b>: hem Play
    /// hem App Store aboneliğin yalnızca kendi abonelik merkezlerinden
    /// bitirilmesine izin veriyor, uygulamanın içinden iptal etmek kural dışı.
    /// O gün burası <see cref="ManagementUri"/> adresini açacak ve dönüşte
    /// durumu mağazaya yeniden soracak.
    /// </para>
    /// <para>
    /// Bugün mağaza yok, bu yüzden yerel uygulama durumu doğrudan
    /// <see cref="SubscriptionStatus.Canceled"/> yapıyor — akışın uçtan uca
    /// denenebilmesi için. Ekranların gördüğü davranış iki durumda da aynı.
    /// </para>
    /// </remarks>
    Task<bool> CancelAsync();

    /// <summary>
    /// Mağazanın abonelik yönetim sayfası. Ebeveyn kilidinin arkasından
    /// açılmalı — uygulamadan dışarı çıkan bir bağlantı.
    /// </summary>
    Uri ManagementUri { get; }
}

/// <summary>
/// Durumu yalnızca cihazda tutan uygulama.
/// </summary>
/// <remarks>
/// <para>
/// Mağaza bağlantısı henüz yok (yol haritasında Faz 4). Bu sınıf o güne kadar
/// iki işi görüyor: kilit/paywall/iptal akışının uçtan uca çalışmasını
/// sağlıyor ve mağaza cevabının nereye yazılacağını sabitliyor —
/// <see cref="SettingKeys.CachedSubscription"/> ve
/// <see cref="SettingKeys.SubscriptionPeriodEnd"/>.
/// </para>
/// <para>
/// Önbellek mağaza geldiğinde de kalacak: uygulama çevrimdışı açıldığında
/// mağazaya sorulamıyor ve çocuğun oyunları "internet yok" diye
/// kilitlenmemeli. Doğruluk kaynağı yine de mağaza olacak; önbellek yalnızca
/// son bilinen değer.
/// </para>
/// </remarks>
public sealed class LocalSubscriptionService(ProgressRepository repository) : ISubscriptionService
{
    /// <summary>
    /// Aylık abonelik. Mağaza geldiğinde ürün kimliği de bu olacak; dönem
    /// uzunluğu buradan okunuyor ki iki yerde ayrışmasın.
    /// </summary>
    public const string PlanProductId = "ploofy_family_monthly";

    private SubscriptionInfo _info = SubscriptionInfo.Free;

    public SubscriptionInfo Info => _info;

    public Entitlements Current => _info.Entitlements;

    public event EventHandler<SubscriptionInfo>? Changed;

    /// <summary>
    /// Play'in abonelik merkezi. Ürün ve paket adı verildiğinde doğrudan bu
    /// aboneliğin sayfası açılıyor; uygulama Play'de yayımlanmadan önce genel
    /// abonelik listesine düşüyor, o da kabul edilebilir.
    /// </summary>
    public Uri ManagementUri { get; } = new(
        "https://play.google.com/store/account/subscriptions" +
        $"?sku={PlanProductId}&package=io.ploofy.app");

    public async Task InitializeAsync()
    {
        var cached = await repository.GetSettingAsync(SettingKeys.CachedSubscription);
        var status = Enum.TryParse<SubscriptionStatus>(cached, out var parsed)
            ? parsed
            : SubscriptionStatus.None;

        Apply(new SubscriptionInfo(status, await ReadPeriodEndAsync()));
    }

    public async Task<bool> PurchaseAsync()
    {
        // Mağaza akışı buraya gelecek. Şimdilik satın alma başarılı sayılıyor
        // ki paywall'dan sonraki kilit açılması gerçek veriyle test edilebilsin.
        var today = DateOnly.FromDateTime(DateTime.Now);
        await SetAsync(new SubscriptionInfo(SubscriptionStatus.Active, today.AddMonths(1)));
        return true;
    }

    public async Task<bool> RestoreAsync()
    {
        // Mağaza gelene kadar geri yüklenecek bir satın alma yok.
        await Task.CompletedTask;
        return _info.Entitlements.HasFullAccess;
    }

    public async Task<bool> CancelAsync()
    {
        if (!_info.Entitlements.CanCancel)
        {
            return false;
        }

        // Dönem sonu olduğu gibi kalıyor: iptal yenilemeyi kapatır, ödenmiş
        // günleri geri almaz.
        await SetAsync(_info with { Status = SubscriptionStatus.Canceled });
        return true;
    }

    /// <summary>
    /// Mağazadan gelen durumu yazar. Mağaza bağlandığında tek çağrı noktası
    /// burası olacak.
    /// </summary>
    public async Task SetAsync(SubscriptionInfo info)
    {
        await repository.SetSettingAsync(SettingKeys.CachedSubscription, info.Status.ToString());
        await repository.SetSettingAsync(
            SettingKeys.SubscriptionPeriodEnd,
            info.PeriodEndsOn?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) ?? string.Empty);

        Apply(info);
    }

    private async Task<DateOnly?> ReadPeriodEndAsync()
    {
        var stored = await repository.GetSettingAsync(SettingKeys.SubscriptionPeriodEnd);

        return DateOnly.TryParseExact(
            stored, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date)
            ? date
            : null;
    }

    private void Apply(SubscriptionInfo info)
    {
        _info = info;
        Changed?.Invoke(this, info);
    }
}
