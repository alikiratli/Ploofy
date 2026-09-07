using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Engine.Access;
using Ploofy.Ui.Parental;

namespace Ploofy.App.ViewModels;

/// <summary>
/// Abonelik yönetimi.
/// </summary>
/// <remarks>
/// <para>
/// Paywall satmak için var, burası <b>yönetmek</b> için: ebeveyn ne durumda
/// olduğunu, ne zamana kadar açık olduğunu ve nasıl bitireceğini burada
/// görüyor. İkisini ayırmanın sebebi, satın alma ekranına "bitir" düğmesi
/// koymanın hem satışı hem yönetimi bozması.
/// </para>
/// <para>
/// Ekran ebeveyn kilidinin arkasındaki ayarlardan açılıyor; para ile ilgili
/// iki eylem (bitirme ve mağazaya çıkma) ayrıca kilit istiyor. Kilit hâlâ
/// açıksa soru tekrar sorulmuyor.
/// </para>
/// </remarks>
public sealed partial class SubscriptionViewModel(
    ISubscriptionService subscriptions,
    IParentalGateService parentalGate) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    [ObservableProperty]
    public partial string BadgeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Color BadgeColor { get; set; } = Colors.Gray;

    [ObservableProperty]
    public partial string StatusText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PeriodText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string PlanText { get; set; } = string.Empty;

    /// <summary>Abone: plan satırı, dönem bilgisi ve "neler dahil" listesi.</summary>
    [ObservableProperty]
    public partial bool HasSubscription { get; set; }

    /// <summary>Abone değil: ücretsiz katman anlatımı ve satın alma düğmesi.</summary>
    [ObservableProperty]
    public partial bool CanSubscribe { get; set; }

    /// <summary>Bitirilecek bir abonelik var mı? "Aboneliği bitir" bölümü buna bağlı.</summary>
    [ObservableProperty]
    public partial bool CanCancel { get; set; }

    /// <summary>Bitirilmiş ama hâlâ açık: "yeniden başlat" öneriliyor.</summary>
    [ObservableProperty]
    public partial bool IsEnding { get; set; }

    [ObservableProperty]
    public partial bool ShowsBillingWarning { get; set; }

    public ObservableCollection<string> Benefits { get; } = [];

    public void Load()
    {
        var l = LocalizationService.Instance;
        var info = subscriptions.Info;
        var entitlements = info.Entitlements;

        HasSubscription = entitlements.HasFullAccess;
        CanSubscribe = !entitlements.HasFullAccess;
        CanCancel = entitlements.CanCancel;
        IsEnding = entitlements.AccessEndsAfterPeriod;
        ShowsBillingWarning = entitlements.NeedsBillingAttention;

        PlanText = l["SubscriptionPlan"];

        (BadgeText, BadgeColor, StatusText) = info.Status switch
        {
            SubscriptionStatus.Active =>
                (l["SubscriptionBadgeActive"], Palette("Leaf"), l["SubscriptionActive"]),
            SubscriptionStatus.Grace =>
                (l["SubscriptionBadgeGrace"], Palette("Retry"), l["SubscriptionGrace"]),
            SubscriptionStatus.Canceled =>
                (l["SubscriptionBadgeCanceled"], Palette("Coral"), l["SubscriptionEndedTitle"]),
            _ =>
                (l["SubscriptionBadgeFree"], Palette("Locked"), l["SubscriptionPitch"]),
        };

        PeriodText = DescribePeriod(info);

        Benefits.Clear();
        Benefits.Add(l["SubscriptionBenefitNoAds"]);
        Benefits.Add(l["SubscriptionBenefitAllGames"]);
        Benefits.Add(l["SubscriptionBenefitNewGames"]);
        Benefits.Add(l.Format("SubscriptionBenefitProfiles", Entitlements.SubscribedProfileLimit));
        Benefits.Add(l["SubscriptionBenefitOffline"]);
    }

    /// <summary>
    /// Dönem cümlesi: yenilenecekse yenileme tarihi, bitirildiyse kapanış
    /// tarihi, ücretsiz katmanda hiçbir şey.
    /// </summary>
    /// <remarks>
    /// Tarih <c>null</c> olabiliyor (çevrimdışı ilk açılış, ya da mağaza henüz
    /// cevaplamadı). O durumda tarih uydurulmuyor — ekran bunu açıkça söylüyor.
    /// </remarks>
    private static string DescribePeriod(SubscriptionInfo info)
    {
        var l = LocalizationService.Instance;

        if (!info.Entitlements.HasFullAccess)
        {
            return string.Empty;
        }

        if (info.PeriodEndsOn is not { } end)
        {
            return l["SubscriptionNoDate"];
        }

        var date = end.ToString("d MMMM yyyy", l.Culture);
        var sentence = info.Entitlements.AccessEndsAfterPeriod
            ? l.Format("SubscriptionEndsOn", date)
            : l.Format("SubscriptionRenewsOn", date);

        var left = info.DaysLeft(DateOnly.FromDateTime(DateTime.Now));
        return left is null ? sentence : $"{sentence} ({l.Format("SubscriptionDaysLeft", left)})";
    }

    private static Color Palette(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Gray;

    [RelayCommand]
    private static async Task SubscribeAsync() => await Shell.Current.GoToAsync("paywall");

    [RelayCommand]
    private async Task RestoreAsync()
    {
        IsBusy = true;
        try
        {
            await subscriptions.RestoreAsync();
            Load();
        }
        finally
        {
            IsBusy = false;
        }
    }

    /// <summary>
    /// Mağazanın abonelik merkezini açar.
    /// </summary>
    /// <remarks>
    /// Fiyat, ödeme yöntemi ve tahsilat tarihi orada yönetiliyor; uygulamanın
    /// kendi hesabı olmadığı için burada gösterilebilecek bir şey de yok.
    /// </remarks>
    [RelayCommand]
    private async Task ManageInStoreAsync()
    {
        if (IsBusy || !await parentalGate.RequestAsync(ParentalGateReason.ExternalLink))
        {
            return;
        }

        await Launcher.Default.OpenAsync(subscriptions.ManagementUri);
    }

    /// <summary>
    /// Aboneliği bitirir.
    /// </summary>
    /// <remarks>
    /// Sıra kasıtlı: önce kilit, sonra onay. Onay ekranını çocuğa hiç
    /// göstermemek, "hayır"a basmasına güvenmekten iyi.
    /// </remarks>
    [RelayCommand]
    private async Task CancelAsync()
    {
        if (IsBusy || !await parentalGate.RequestAsync(ParentalGateReason.Purchase))
        {
            return;
        }

        var l = LocalizationService.Instance;

        // Tarih onay kutusunda da yazıyor: "dönem sonuna kadar açık kalır"
        // cümlesi tek başına ebeveyne bir şey söylemiyor; karar verirken
        // bilmek istediği şey hangi güne kadar olduğu.
        var confirmed = await Shell.Current.DisplayAlertAsync(
            l["SubscriptionEndConfirm"],
            $"{l["SubscriptionEndBody"]}{UntilSentence(subscriptions.Info)}"
                + $"\n\n{l["SubscriptionEndKeeps"]}",
            l["SubscriptionEndTitle"],
            l["CommonCancel"]);

        if (!confirmed)
        {
            return;
        }

        IsBusy = true;
        try
        {
            await subscriptions.CancelAsync();
            Load();
        }
        finally
        {
            IsBusy = false;
        }

        await AnnounceEndAsync();
    }

    /// <summary>
    /// İptalden sonra ne olduğunu tek cümleyle söyler.
    /// </summary>
    /// <remarks>
    /// Ekrandaki rozet ve tarih zaten güncelleniyor, ama ebeveyn "bitir"e
    /// bastıktan sonra gözü ekranın o köşesinde değil. Bu kutu, kararın
    /// karşılığını kararın verildiği yerde gösteriyor.
    ///
    /// Ödenmiş dönem çoktan dolmuşsa erişim iptal anında kapanıyor
    /// (<c>SubscriptionInfo.AsOf</c>); orada tarih söylemek yanlış olurdu,
    /// cümle "kilitler geri geldi" diyor.
    /// </remarks>
    private async Task AnnounceEndAsync()
    {
        var l = LocalizationService.Instance;
        var info = subscriptions.Info;

        var body = info.Entitlements.HasFullAccess && info.PeriodEndsOn is { } end
            ? l.Format("SubscriptionEndsOnLong", FormatDate(end))
            : l["SubscriptionEndedNow"];

        await Shell.Current.DisplayAlertAsync(
            l["SubscriptionEndedTitle"], body, l["CommonOk"]);
    }

    /// <summary>Onay kutusuna eklenen tarih cümlesi; tarih bilinmiyorsa boş.</summary>
    private static string UntilSentence(SubscriptionInfo info) =>
        info.PeriodEndsOn is { } end
            ? "\n\n" + LocalizationService.Instance.Format(
                "SubscriptionEndsOnLong", FormatDate(end))
            : string.Empty;

    private static string FormatDate(DateOnly date) =>
        date.ToString("d MMMM yyyy", LocalizationService.Instance.Culture);

    [RelayCommand]
    private static async Task CloseAsync() => await Shell.Current.GoToAsync("..");
}
