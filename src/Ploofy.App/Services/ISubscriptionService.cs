using Ploofy.Data;
using Ploofy.Engine.Access;

namespace Ploofy.App.Services;

/// <summary>
/// Aboneliğin durumunu bildiren ve satın almayı başlatan katman.
/// </summary>
/// <remarks>
/// Uygulamanın geri kalanı yalnızca bu arayüzü tanıyor. Mağaza bağlantısı
/// (Play Billing / StoreKit) buranın arkasına takılacak; ekranlar ve
/// <see cref="Entitlements"/> mantığı o gün değişmeyecek.
/// </remarks>
public interface ISubscriptionService
{
    Entitlements Current { get; }

    event EventHandler<Entitlements>? Changed;

    /// <summary>Açılışta çağrılır: son bilinen durumu yükler.</summary>
    Task InitializeAsync();

    /// <summary>Satın alma akışını başlatır. Ebeveyn kilidinin arkasından çağrılmalı.</summary>
    Task<bool> PurchaseAsync();

    /// <summary>Mağazadaki mevcut satın alımları geri yükler.</summary>
    Task<bool> RestoreAsync();
}

/// <summary>
/// Durumu yalnızca cihazda tutan uygulama.
/// </summary>
/// <remarks>
/// <para>
/// Mağaza bağlantısı henüz yok (yol haritasında Faz 3). Bu sınıf o güne kadar
/// iki işi görüyor: kilit/paywall akışının uçtan uca çalışmasını sağlıyor ve
/// mağaza cevabının nereye yazılacağını sabitliyor —
/// <see cref="SettingKeys.CachedSubscription"/>.
/// </para>
/// <para>
/// Önbellek mağaza geldiğinde de kalacak: uygulama çevrimdışı açıldığında
/// mağazaya sorulamıyor ve çocuğun oyunları "internet yok" diye kilitlenmemeli.
/// Doğruluk kaynağı yine de mağaza olacak; önbellek yalnızca son bilinen değer.
/// </para>
/// </remarks>
public sealed class LocalSubscriptionService(ProgressRepository repository) : ISubscriptionService
{
    private Entitlements _current = Entitlements.Free;

    public Entitlements Current => _current;

    public event EventHandler<Entitlements>? Changed;

    public async Task InitializeAsync()
    {
        var cached = await repository.GetSettingAsync(SettingKeys.CachedSubscription);
        Apply(Enum.TryParse<SubscriptionStatus>(cached, out var status)
            ? status
            : SubscriptionStatus.None);
    }

    public async Task<bool> PurchaseAsync()
    {
        // Mağaza akışı buraya gelecek. Şimdilik satın alma başarılı sayılıyor
        // ki paywall'dan sonraki kilit açılması gerçek veriyle test edilebilsin.
        await SetStatusAsync(SubscriptionStatus.Active);
        return true;
    }

    public async Task<bool> RestoreAsync()
    {
        // Mağaza gelene kadar geri yükleyecek bir satın alma yok.
        await Task.CompletedTask;
        return _current.HasFullAccess;
    }

    /// <summary>
    /// Mağazadan gelen durumu yazar. Mağaza bağlandığında tek çağrı noktası
    /// burası olacak.
    /// </summary>
    public async Task SetStatusAsync(SubscriptionStatus status)
    {
        await repository.SetSettingAsync(SettingKeys.CachedSubscription, status.ToString());
        Apply(status);
    }

    private void Apply(SubscriptionStatus status)
    {
        _current = new Entitlements(status);
        Changed?.Invoke(this, _current);
    }
}
