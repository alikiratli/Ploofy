using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Engine;
using Ploofy.Engine.Catalog;
using Ploofy.Engine.Sessions;
using Ploofy.Ui.Feedback;

namespace Ploofy.App.ViewModels;

/// <summary>Ana ekrandaki tek oyun kutucuğu.</summary>
public sealed class GameTile(
    MiniGameDescriptor game,
    int stars,
    bool isLocked,
    bool isPlayable)
{
    public MiniGameDescriptor Game { get; } = game;

    public string GameId => Game.Id;

    public string Name => GamePresentation.Name(Game.Id);

    public string Glyph => GamePresentation.Glyph(Game.Id);

    public Brush Background =>
        Application.Current?.Resources.TryGetValue(
            GamePresentation.BackgroundKey(Game.Id), out var value) == true
            ? (Brush)value
            : Brush.White;

    /// <summary>Bu bantta bu oyundan alınmış en iyi yıldız.</summary>
    public int Stars { get; } = stars;

    public bool IsLocked { get; } = isLocked;

    /// <summary>Oyunun sayfası yazıldı mı? Yazılmadıysa kutucuk "yakında" diyor.</summary>
    public bool IsPlayable { get; } = isPlayable;

    public bool ShowsFreeBadge => Game.Tier == GameTier.Free;

    public bool ShowsComingSoon => !IsLocked && !IsPlayable;

    /// <summary>Kilitli ya da henüz yazılmamış kutucuk soluk görünüyor.</summary>
    public double TileOpacity => IsLocked || !IsPlayable ? 0.55 : 1.0;
}

/// <summary>
/// Ana ekran: seçili çocuğun bandına uygun oyunlar.
/// </summary>
/// <remarks>
/// Kilitli oyunlar gizlenmiyor, soluk gösteriliyor. Gizlemek "abonelik ne
/// getiriyor" sorusunu cevapsız bırakıyor; göstermek ise çocuğun kilitli
/// kutucuğa dokunup ebeveyni çağırmasına yol açıyor — istenen de bu.
/// </remarks>
public sealed partial class HomeViewModel(
    ProgressRepository repository,
    AppState state,
    PlayFlow flow,
    IFeedbackService feedback) : ObservableObject
{
    [ObservableProperty]
    public partial string ChildName { get; set; }

    [ObservableProperty]
    public partial string ChildAvatar { get; set; }

    [ObservableProperty]
    public partial int TotalStars { get; set; }

    /// <summary>
    /// Filiz bandında öğretici oyun yok; başlığı boş bir bölümün üstünde
    /// göstermemek için.
    /// </summary>
    [ObservableProperty]
    public partial bool HasEducationalGames { get; set; }

    public ObservableCollection<GameTile> FunGames { get; } = [];

    public ObservableCollection<GameTile> EducationalGames { get; } = [];

    [RelayCommand]
    public async Task LoadAsync()
    {
        var profile = state.ActiveProfile;
        if (profile is null)
        {
            // Profil seçilmeden bu ekrana gelinmiş: seçim ekranına dön.
            await Shell.Current.GoToAsync("//profiles");
            return;
        }

        ChildName = profile.DisplayName;
        ChildAvatar = profile.AvatarId;
        TotalStars = await repository.TotalStarsAsync(profile.Id);

        var band = state.ActiveBand;
        var progress = await repository.ProgressForAsync(profile.Id);
        var entitlements = state.Entitlements;

        FunGames.Clear();
        EducationalGames.Clear();

        foreach (var game in GameCatalog.ForBand(band))
        {
            var stars = progress
                .Where(p => p.GameId == game.Id && p.AgeBandId == band.ToId())
                .Select(p => p.BestStars)
                .DefaultIfEmpty(0)
                .Max();

            var tile = new GameTile(
                game,
                stars,
                isLocked: !entitlements.CanPlay(game),
                isPlayable: GamePresentation.IsPlayable(game.Id));

            if (game.IsEducational)
            {
                EducationalGames.Add(tile);
            }
            else
            {
                FunGames.Add(tile);
            }
        }

        HasEducationalGames = EducationalGames.Count > 0;
    }

    [RelayCommand]
    private async Task OpenAsync(GameTile? tile)
    {
        if (tile is null)
        {
            return;
        }

        if (tile.IsLocked)
        {
            await feedback.PlayAsync(FeedbackCue.Locked);
            await Shell.Current.GoToAsync("paywall");
            return;
        }

        if (!tile.IsPlayable)
        {
            var l = LocalizationService.Instance;
            await Shell.Current.DisplayAlert(tile.Name, l["CommonNotYet"], l["CommonOk"]);
            return;
        }

        await feedback.PlayAsync(FeedbackCue.Tap);
        flow.SelectedGameId = tile.GameId;

        // Kurulum ekranı yalnızca seçilecek bir şey varken gösteriliyor.
        // Tek profilli cihazda ya da sıraya bölünemeyen bir oyunda çocuğu
        // anlamsız bir ara adımdan geçirmek yerine doğrudan oyuna giriliyor.
        if (await NeedsSetupAsync(tile.Game))
        {
            await Shell.Current.GoToAsync("playsetup");
            return;
        }

        var player = state.ActivePlayer;
        var route = GamePresentation.Route(tile.GameId);
        if (player is null || route is null)
        {
            return;
        }

        flow.PendingSession = GameSession.Solo(tile.GameId, player);
        await Shell.Current.GoToAsync(route);
    }

    private async Task<bool> NeedsSetupAsync(MiniGameDescriptor game)
    {
        if (!game.SupportsPassAndPlay || !state.Entitlements.CanUseMultipleProfilesInSession)
        {
            return false;
        }

        var profiles = await repository.ListProfilesAsync();
        return profiles.Count >= 2;
    }

    [RelayCommand]
    private async Task SwitchProfileAsync()
    {
        // Profil değiştirmek kilit gerektirmiyor: kardeşin sırası geldiğinde
        // her seferinde ebeveyn çağırmak akışı kırıyor.
        await Shell.Current.GoToAsync("//profiles");
    }
}
