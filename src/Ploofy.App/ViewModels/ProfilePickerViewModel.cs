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

/// <summary>Profil kartında gösterilen tek çocuk.</summary>
public sealed partial class ProfileCard(ChildProfileRow row, int totalStars) : ObservableObject
{
    public ChildProfileRow Row { get; } = row;

    public int Id => Row.Id;

    public string DisplayName => Row.DisplayName;

    public string AvatarId => Row.AvatarId;

    public AgeBand Band => AgeBandExtensions.FromId(Row.AgeBandId);

    public string BandName => LocalizationService.Instance[Band switch
    {
        AgeBand.Filiz => "BandFiliz",
        AgeBand.Fidan => "BandFidan",
        _ => "BandMese",
    }];

    public int TotalStars { get; } = totalStars;
}

/// <summary>
/// Açılış ekranı: kim oynuyor?
/// </summary>
/// <remarks>
/// Profil ekleme ve silme ebeveyn kilidinin arkasında; profil <b>seçmek</b>
/// değil. Çocuk kendi kartına dokunup oyuna girebilmeli, her açılışta ebeveyn
/// çağırmak zorunda kalmamalı.
/// </remarks>
public sealed partial class ProfilePickerViewModel(
    ProgressRepository repository,
    AppState state,
    IParentalGateService parentalGate,
    IFeedbackService feedback) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsBusy { get; set; }

    /// <summary>Ücretsiz katmanda ikinci profil eklenemiyor; ekran bunu söylüyor.</summary>
    [ObservableProperty]
    public partial bool CanAddProfile { get; set; }

    [ObservableProperty]
    public partial bool ShowsProfileLimitNote { get; set; }

    public ObservableCollection<ProfileCard> Profiles { get; } = [];

    [RelayCommand]
    public async Task LoadAsync()
    {
        IsBusy = true;
        try
        {
            Profiles.Clear();

            foreach (var row in await repository.ListProfilesAsync())
            {
                Profiles.Add(new ProfileCard(row, await repository.TotalStarsAsync(row.Id)));
            }

            CanAddProfile = state.Entitlements.CanAddProfile(Profiles.Count);

            // Sınır yalnızca profili olan ve daha fazlasını isteyene anlatılıyor;
            // hiç profili olmayan ebeveyne kısıtla başlamak yanlış karşılama.
            ShowsProfileLimitNote = !CanAddProfile && Profiles.Count > 0;
        }
        finally
        {
            IsBusy = false;
        }
    }

    [RelayCommand]
    private async Task SelectAsync(ProfileCard? card)
    {
        if (card is null)
        {
            return;
        }

        await feedback.PlayAsync(FeedbackCue.Tap);
        await state.SetActiveProfileAsync(card.Row);
        await Shell.Current.GoToAsync("home");
    }

    [RelayCommand]
    private async Task AddAsync()
    {
        if (!CanAddProfile)
        {
            await feedback.PlayAsync(FeedbackCue.Locked);
            await Shell.Current.GoToAsync("paywall");
            return;
        }

        // Yeni profil oluşturmak bir ebeveyn işi: ad yazmak, yaş seçmek.
        if (!await parentalGate.RequestAsync(ParentalGateReason.ProfileManagement))
        {
            return;
        }

        await Shell.Current.GoToAsync("profileeditor");
    }

    [RelayCommand]
    private async Task OpenSettingsAsync()
    {
        if (!await parentalGate.RequestAsync(ParentalGateReason.Settings))
        {
            return;
        }

        await Shell.Current.GoToAsync("settings");
    }
}
