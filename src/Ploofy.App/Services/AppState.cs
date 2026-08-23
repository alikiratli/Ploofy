using Ploofy.App.Localization;
using Ploofy.Data;
using Ploofy.Engine;
using Ploofy.Engine.Access;
using Ploofy.Engine.Sessions;
using Ploofy.Ui.Feedback;

namespace Ploofy.App.Services;

/// <summary>
/// Uygulamanın oturum durumu: hangi çocuk oynuyor, neye erişebiliyor.
/// </summary>
/// <remarks>
/// Ekranlar "seçili profil kim?" sorusunu tek bir yere soruyor. Profil
/// değiştiğinde bant da değişiyor — ana ekrandaki oyun listesi, zorluk ve
/// yıldızlar hep buradan türüyor.
/// </remarks>
public sealed class AppState(
    ProgressRepository repository,
    ISubscriptionService subscriptions,
    IFeedbackService feedback)
{
    public ChildProfileRow? ActiveProfile { get; private set; }

    public Entitlements Entitlements => subscriptions.Current;

    /// <summary>Seçili çocuğun bandı; profil yoksa orta bant.</summary>
    public AgeBand ActiveBand => ActiveProfile is null
        ? AgeBand.Fidan
        : AgeBandExtensions.FromId(ActiveProfile.AgeBandId);

    public Player? ActivePlayer =>
        ActiveProfile is null ? null : ProgressRepository.ToPlayer(ActiveProfile);

    public event EventHandler? ActiveProfileChanged;

    /// <summary>
    /// Açılış sırası: ayarlar (dil, ses) → abonelik durumu → son seçili profil.
    /// </summary>
    public async Task InitializeAsync()
    {
        LocalizationService.Instance.ApplySavedOrDeviceLanguage(
            await repository.GetSettingAsync(SettingKeys.Locale));

        feedback.SoundEnabled =
            await repository.GetBoolSettingAsync(SettingKeys.SoundEnabled, orElse: true);
        feedback.HapticsEnabled =
            await repository.GetBoolSettingAsync(SettingKeys.HapticsEnabled, orElse: true);

        await subscriptions.InitializeAsync();

        var savedId = await repository.GetSettingAsync(SettingKeys.ActiveProfile);
        if (int.TryParse(savedId, out var id))
        {
            ActiveProfile = await repository.ProfileByIdAsync(id);
        }

        // Tek profil varsa onu seç: her açılışta aynı tek kartı seçtirmek
        // gereksiz bir adım.
        if (ActiveProfile is null)
        {
            var profiles = await repository.ListProfilesAsync();
            if (profiles.Count == 1)
            {
                ActiveProfile = profiles[0];
            }
        }

        ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SetActiveProfileAsync(ChildProfileRow? profile)
    {
        ActiveProfile = profile;

        if (profile is null)
        {
            await repository.SetSettingAsync(SettingKeys.ActiveProfile, string.Empty);
        }
        else
        {
            await repository.SetSettingAsync(
                SettingKeys.ActiveProfile,
                profile.Id.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
    }

    /// <summary>Seçili profil silindiğinde ya da değiştiğinde tazeler.</summary>
    public async Task RefreshActiveProfileAsync()
    {
        if (ActiveProfile is null)
        {
            return;
        }

        ActiveProfile = await repository.ProfileByIdAsync(ActiveProfile.Id);
        ActiveProfileChanged?.Invoke(this, EventArgs.Empty);
    }
}
