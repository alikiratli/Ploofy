using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Engine;
using Ploofy.Engine.Access;
using Ploofy.Ui.Feedback;

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

    [ObservableProperty]
    public partial bool ShowsBillingWarning { get; set; }

    [ObservableProperty]
    public partial bool CanSubscribe { get; set; }

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
        var entitlements = subscriptions.Current;

        CanSubscribe = !entitlements.HasFullAccess;
        ShowsBillingWarning = entitlements.NeedsBillingAttention;

        SubscriptionStatusText = entitlements.Status switch
        {
            SubscriptionStatus.Active => l["SubscriptionActive"],
            SubscriptionStatus.Grace => l["SubscriptionGrace"],
            _ => l["SubscriptionPitch"],
        };
    }

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
        var confirmed = await Shell.Current.DisplayAlert(
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

    [RelayCommand]
    private static async Task OpenPaywallAsync() => await Shell.Current.GoToAsync("paywall");

    [RelayCommand]
    private async Task RestoreAsync()
    {
        await subscriptions.RestoreAsync();
        ApplyEntitlements();
    }

    [RelayCommand]
    private static async Task CloseAsync() => await Shell.Current.GoToAsync("..");
}
