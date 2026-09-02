using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Engine;
using Ploofy.Engine.Access;
using Ploofy.Ui.Feedback;
using Ploofy.Ui.Parental;

namespace Ploofy.App.ViewModels;

/// <summary>Dil seçeneği.</summary>
public sealed record LanguageOption(string Code, string Label);

/// <summary>Ayarlar ekranındaki profil satırı.</summary>
public sealed class ProfileRow(ChildProfileRow row)
{
    public ChildProfileRow Row { get; } = row;

    public string DisplayName => Row.DisplayName;

    public string AvatarId => Row.AvatarId;

    public string BandName => LocalizationService.Instance[
        AgeBandExtensions.FromId(Row.AgeBandId) switch
        {
            AgeBand.Filiz => "BandFiliz",
            AgeBand.Fidan => "BandFidan",
            _ => "BandMese",
        }];
}

/// <summary>
/// Ebeveyn ekranı.
/// </summary>
/// <remarks>
/// Ebeveyn kilidinin arkasından açılıyor. Buradaki her şey ebeveynin işi:
/// dil, ses, profil silme, abonelik. Çocuğun ihtiyaç duyduğu hiçbir ayar
/// burada değil — kilit çocuğu oyundan koparmıyor.
/// </remarks>
public sealed partial class SettingsViewModel(
    ProgressRepository repository,
    AppState state,
    ISubscriptionService subscriptions,
    IParentalGateService parentalGate,
    IFeedbackService feedback) : ObservableObject
{
    public static readonly IReadOnlyList<LanguageOption> Languages =
    [
        new("tr", "Türkçe"),
        new("en", "English"),
        new("de", "Deutsch"),
    ];

    [ObservableProperty]
    public partial bool SoundEnabled { get; set; }

    [ObservableProperty]
    public partial bool HapticsEnabled { get; set; }

    [ObservableProperty]
    public partial LanguageOption SelectedLanguage { get; set; }

    [ObservableProperty]
    public partial string SubscriptionStatusText { get; set; }

    /// <summary>Abonelik kartındaki rozetin metni ve rengi.</summary>
    [ObservableProperty]
    public partial string SubscriptionBadgeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial Color SubscriptionBadgeColor { get; set; } = Colors.Gray;

    /// <summary>"{tarih} tarihinde yenilenir" gibi tek satır; aboneliksizken boş.</summary>
    [ObservableProperty]
    public partial string SubscriptionPeriodText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasSubscription { get; set; }

    [ObservableProperty]
    public partial bool ShowsBillingWarning { get; set; }

    [ObservableProperty]
    public partial bool CanSubscribe { get; set; }

    /// <summary>Mağaza girişindeki sürüm; destek yazışmasında ilk sorulan şey.</summary>
    public string VersionText =>
        LocalizationService.Instance.Format("SettingsVersion", AppInfo.Current.VersionString);

    public ObservableCollection<LanguageOption> LanguageChoices { get; } = [.. Languages];

    public ObservableCollection<ProfileRow> Profiles { get; } = [];

    public async Task LoadAsync()
    {
        SoundEnabled = feedback.SoundEnabled;
        HapticsEnabled = feedback.HapticsEnabled;

        var current = LocalizationService.Instance.CurrentLanguage;
        SelectedLanguage = Languages.FirstOrDefault(l => l.Code == current) ?? Languages[1];

        Profiles.Clear();
        foreach (var row in await repository.ListProfilesAsync())
        {
            Profiles.Add(new ProfileRow(row));
        }

        ApplyEntitlements();
    }

    private void ApplyEntitlements()
    {
        var l = LocalizationService.Instance;
        var info = subscriptions.Info;
        var entitlements = info.Entitlements;

        HasSubscription = entitlements.HasFullAccess;
        CanSubscribe = !entitlements.HasFullAccess;
        ShowsBillingWarning = entitlements.NeedsBillingAttention;

        (SubscriptionBadgeText, SubscriptionBadgeColor, SubscriptionStatusText) = info.Status switch
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

        // Tarih yalnızca bir satır: ayrıntı ve bitirme yolu abonelik
        // ekranında. Ayarlar sayfası özet olmalı, ikinci bir abonelik
        // ekranı değil.
        SubscriptionPeriodText = info.PeriodEndsOn is { } end && entitlements.HasFullAccess
            ? l.Format(
                entitlements.AccessEndsAfterPeriod ? "SubscriptionEndsOn" : "SubscriptionRenewsOn",
                end.ToString("d MMMM yyyy", l.Culture))
            : string.Empty;

        OnPropertyChanged(nameof(VersionText));
    }

    private static Color Palette(string key) =>
        Application.Current?.Resources.TryGetValue(key, out var value) == true && value is Color color
            ? color
            : Colors.Gray;

    partial void OnSoundEnabledChanged(bool value)
    {
        feedback.SoundEnabled = value;
        _ = repository.SetBoolSettingAsync(SettingKeys.SoundEnabled, value);
    }

    partial void OnHapticsEnabledChanged(bool value)
    {
        feedback.HapticsEnabled = value;
        _ = repository.SetBoolSettingAsync(SettingKeys.HapticsEnabled, value);
    }

    partial void OnSelectedLanguageChanged(LanguageOption value)
    {
        LocalizationService.Instance.SetLanguage(value.Code);
        _ = repository.SetSettingAsync(SettingKeys.Locale, value.Code);

        // Dil değişince bu ekrandaki kendi metinlerimiz de tazelensin.
        ApplyEntitlements();
        OnPropertyChanged(nameof(Profiles));
    }

    /// <summary>
    /// Profili düzenleme ekranına götürür.
    /// </summary>
    /// <remarks>
    /// Ayrı bir ebeveyn kilidi istemiyor: bu ekran zaten kilidin arkasında ve
    /// çocuğu kilit sorusuyla ikinci kez karşılamanın koruduğu bir şey yok.
    /// </remarks>
    [RelayCommand]
    private static async Task EditProfileAsync(ProfileRow? row)
    {
        if (row is null)
        {
            return;
        }

        await Shell.Current.GoToAsync(
            $"profileeditor?{ProfileEditorViewModel.ProfileIdParameter}={row.Row.Id}");
    }

    [RelayCommand]
    private async Task DeleteProfileAsync(ProfileRow? row)
    {
        if (row is null)
        {
            return;
        }

        var l = LocalizationService.Instance;
        var confirmed = await Shell.Current.DisplayAlertAsync(
            l["ProfileDelete"],
            l.Format("ProfileDeleteConfirm", row.DisplayName),
            l["CommonOk"],
            l["CommonCancel"]);

        if (!confirmed)
        {
            return;
        }

        await repository.DeleteProfileAsync(row.Row.Id);
        Profiles.Remove(row);

        // Silinen profil seçili olansa seçim boşaltılıyor; aksi halde ana ekran
        // artık var olmayan bir çocuğun yıldızlarını göstermeye çalışırdı.
        if (state.ActiveProfile?.Id == row.Row.Id)
        {
            await state.SetActiveProfileAsync(null);
        }
    }

    /// <summary>
    /// Abonelik yönetimine götürür.
    /// </summary>
    /// <remarks>
    /// Aboneliksizken doğrudan paywall'a gitmiyor: yönetim ekranı ücretsiz
    /// katmanda ne kaldığını da anlatıyor ve satın alma düğmesi orada da var.
    /// Tek kapı olması, "bitir" yolunun her durumda aynı yerde durmasını
    /// sağlıyor.
    /// </remarks>
    [RelayCommand]
    private static async Task ManageSubscriptionAsync() =>
        await Shell.Current.GoToAsync("subscription");

    /// <summary>
    /// Oyun raporuna götürür.
    /// </summary>
    /// <remarks>
    /// Ayrı bir ebeveyn kilidi istemiyor: bu ekran zaten kilidin arkasında ve
    /// rapor cihazdan hiçbir şey çıkarmıyor.
    /// </remarks>
    [RelayCommand]
    private static async Task OpenReportAsync() => await Shell.Current.GoToAsync("report");

    /// <summary>
    /// Geri bildirim seslerinden ikisini çalar.
    /// </summary>
    /// <remarks>
    /// Ebeveynin sesi cihazın hoparlöründe duymadan "açık kalsın mı"
    /// diye karar vermesi mümkün değil — anahtarın hemen yanında bir
    /// örnek olması bu kararı ayarlar ekranında bitiriyor. İkisi
    /// seçildi çünkü aralarındaki seviye farkı en belirgin olan bunlar:
    /// yıldız sesi tizden çıkıyor, doğru sesi orta bantta.
    /// </remarks>
    [RelayCommand]
    private async Task PlaySoundSampleAsync()
    {
        await feedback.PlayAsync(FeedbackCue.Correct);
        await Task.Delay(TimeSpan.FromMilliseconds(400));
        await feedback.PlayAsync(FeedbackCue.StarEarned);
    }

    /// <summary>
    /// Gizlilik politikasını tarayıcıda açar.
    /// </summary>
    /// <remarks>
    /// Uygulamadan dışarı çıkan ilk bağlantı bu; kilit gerekçesi
    /// <see cref="ParentalGateReason.ExternalLink"/> bugüne kadar hiç
    /// çağrılmamıştı. Ayarlar zaten kilidin arkasında ama kilit beş dakika
    /// açık kalıyor: tarayıcıya çıkmak ayrı bir eşik.
    /// </remarks>
    [RelayCommand]
    private Task OpenPrivacyPolicyAsync() => OpenLinkAsync(PloofyLinks.PrivacyPolicy);

    [RelayCommand]
    private Task OpenImprintAsync() => OpenLinkAsync(PloofyLinks.Imprint);

    private async Task OpenLinkAsync(Uri uri)
    {
        if (!await parentalGate.RequestAsync(ParentalGateReason.ExternalLink))
        {
            return;
        }

        await Browser.Default.OpenAsync(uri, BrowserLaunchMode.SystemPreferred);
    }

    [RelayCommand]
    private static async Task CloseAsync() => await Shell.Current.GoToAsync("..");
}
