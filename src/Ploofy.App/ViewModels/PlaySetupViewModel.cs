using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Engine.Catalog;
using Ploofy.Engine.Sessions;
using Ploofy.Ui.Feedback;

namespace Ploofy.App.ViewModels;

/// <summary>Oynanış modu seçeneği.</summary>
public sealed partial class ModeOption(SessionMode mode, string titleKey, string? hintKey)
    : ObservableObject
{
    public SessionMode Mode { get; } = mode;

    public string Title => LocalizationService.Instance[titleKey];

    public string? Hint => hintKey is null ? null : LocalizationService.Instance[hintKey];

    public bool HasHint => hintKey is not null;

    /// <summary>Henüz gelmemiş modlar listede duruyor ama seçilemiyor.</summary>
    public bool IsAvailable { get; init; } = true;

    public string? ComingSoonLabel =>
        IsAvailable ? null : LocalizationService.Instance["ComingSoon"];

    public double Opacity => IsAvailable ? 1.0 : 0.5;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

}

/// <summary>Sıralı oyunda masaya oturan çocuk.</summary>
public sealed partial class PlayerChoice(ChildProfileRow row) : ObservableObject
{
    public ChildProfileRow Row { get; } = row;

    public string DisplayName => Row.DisplayName;

    public string AvatarId => Row.AvatarId;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

}

/// <summary>
/// Oyuna girmeden önceki tek soru: nasıl oynayacağız?
/// </summary>
/// <remarks>
/// Bu ekran yalnızca seçilecek bir şey varken açılıyor. Tek profilli cihazda
/// ya da sıraya bölünemeyen bir oyunda ana ekran doğrudan oyuna giriyor —
/// kararı <see cref="HomeViewModel"/> veriyor, böylece kullanılmayan bir sayfa
/// geri yığınında durup geri tuşunu bozmuyor.
/// </remarks>
public sealed partial class PlaySetupViewModel(
    ProgressRepository repository,
    AppState state,
    PlayFlow flow,
    IFeedbackService feedback) : ObservableObject
{
    [ObservableProperty]
    public partial string GameName { get; set; }

    [ObservableProperty]
    public partial string GameGlyph { get; set; }

    [ObservableProperty]
    public partial bool ShowsPlayerPicker { get; set; }

    [ObservableProperty]
    public partial bool CanStart { get; set; }

    public ObservableCollection<ModeOption> Modes { get; } = [];

    public ObservableCollection<PlayerChoice> Players { get; } = [];

    public async Task LoadAsync()
    {
        var gameId = flow.SelectedGameId;
        if (gameId is null || GameCatalog.TryById(gameId) is not { } game)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        GameName = GamePresentation.Name(gameId);
        GameGlyph = GamePresentation.Glyph(gameId);

        var profiles = await repository.ListProfilesAsync();
        var entitlements = state.Entitlements;

        var canPlayTogether =
            game.SupportsPassAndPlay &&
            profiles.Count >= 2 &&
            entitlements.CanUseMultipleProfilesInSession;

        BuildModes(canPlayTogether);

        Players.Clear();
        foreach (var profile in profiles)
        {
            var choice = new PlayerChoice(profile)
            {
                // Sıra seçili çocukla başlasın: ana ekranda kim oynuyorsa
                // oyuna da o girsin.
                IsSelected = profile.Id == state.ActiveProfile?.Id,
            };
            choice.PropertyChanged += (_, _) => UpdateCanStart();
            Players.Add(choice);
        }

        UpdateCanStart();
    }

    private void BuildModes(bool canPlayTogether)
    {
        Modes.Clear();

        Modes.Add(new ModeOption(SessionMode.Solo, "PlaySolo", null) { IsSelected = true });

        if (canPlayTogether)
        {
            Modes.Add(new ModeOption(SessionMode.PassAndPlay, "PlayTogether", "PlayTogetherHint"));
        }

        // Henüz gelmemiş modlar listede duruyor: yol haritası ürünün içinden
        // görünüyor ve "bu uygulama başka cihazla da oynanacak mı?" sorusu
        // cevapsız kalmıyor.
        Modes.Add(new ModeOption(SessionMode.LocalNetwork, "PlayLocalNetwork", null)
        {
            IsAvailable = false,
        });
        Modes.Add(new ModeOption(SessionMode.FamilyLink, "PlayFamilyLink", null)
        {
            IsAvailable = false,
        });

        foreach (var mode in Modes)
        {
            mode.PropertyChanged += (sender, args) =>
            {
                if (args.PropertyName != nameof(ModeOption.IsSelected))
                {
                    return;
                }

                if (sender is ModeOption { IsSelected: true } selected)
                {
                    ShowsPlayerPicker = selected.Mode == SessionMode.PassAndPlay;
                    UpdateCanStart();
                }
            };
        }
    }

    private ModeOption? SelectedMode => Modes.FirstOrDefault(m => m.IsSelected);

    private void UpdateCanStart()
    {
        var mode = SelectedMode;
        if (mode is null || !mode.IsAvailable)
        {
            CanStart = false;
            return;
        }

        CanStart = mode.Mode != SessionMode.PassAndPlay
            || Players.Count(p => p.IsSelected) >= 2;
    }

    [RelayCommand]
    private async Task SelectModeAsync(ModeOption? option)
    {
        if (option is null)
        {
            return;
        }

        if (!option.IsAvailable)
        {
            var l = LocalizationService.Instance;
            await Shell.Current.DisplayAlert(option.Title, l["CommonNotYet"], l["CommonOk"]);
            return;
        }

        foreach (var mode in Modes)
        {
            mode.IsSelected = ReferenceEquals(mode, option);
        }

        await feedback.PlayAsync(FeedbackCue.Tap);
    }

    [RelayCommand]
    private void TogglePlayer(PlayerChoice? choice)
    {
        if (choice is not null)
        {
            choice.IsSelected = !choice.IsSelected;
        }
    }

    [RelayCommand]
    public async Task StartAsync()
    {
        var gameId = flow.SelectedGameId;
        var route = gameId is null ? null : GamePresentation.Route(gameId);
        if (gameId is null || route is null)
        {
            return;
        }

        var mode = SelectedMode?.Mode ?? SessionMode.Solo;

        if (mode == SessionMode.PassAndPlay)
        {
            var chosen = Players
                .Where(p => p.IsSelected)
                .Select(p => ProgressRepository.ToPlayer(p.Row))
                .ToList();

            if (chosen.Count < 2)
            {
                return;
            }

            flow.PendingSession = new GameSession(gameId, SessionMode.PassAndPlay, chosen);
        }
        else
        {
            var player = state.ActivePlayer;
            if (player is null)
            {
                return;
            }

            flow.PendingSession = GameSession.Solo(gameId, player);
        }

        await Shell.Current.GoToAsync(route);
    }
}
