using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Engine.Difficulty;
using Ploofy.Engine.Sessions;

namespace Ploofy.App.ViewModels;

/// <summary>
/// Oyun sonu ekranı.
/// </summary>
/// <remarks>
/// Tek kişilik oyunda yalnızca kazanılan yıldızlar gösteriliyor — sayı yok,
/// kıyas yok, kendi turu. Sıralı oyunda ise iki çocuğun satırı yan yana;
/// beraberlikte "ikiniz de kazandınız" yazıyor, çünkü kardeşler arasında
/// berabere biten bir oyunu kaybeden aramak gereksiz.
/// </remarks>
public sealed partial class RoundResultViewModel(
    PlayFlow flow,
    ProgressRepository repository,
    AppState state) : ObservableObject
{
    [ObservableProperty]
    public partial string GameName { get; set; }

    [ObservableProperty]
    public partial string Headline { get; set; }

    [ObservableProperty]
    public partial bool IsMultiplayer { get; set; }

    [ObservableProperty]
    public partial int SoloStars { get; set; }

    /// <summary>Bu turla açılan avatarlar; yoksa şerit hiç görünmüyor.</summary>
    public ObservableCollection<string> UnlockedAvatars { get; } = [];

    [ObservableProperty]
    public partial bool HasUnlock { get; set; }

    [ObservableProperty]
    public partial string UnlockText { get; set; } = string.Empty;

    /// <summary>
    /// Oyun süresiyle ilgili haber; sınır yoksa ya da daha çok varsa boş.
    /// </summary>
    /// <remarks>
    /// İki hâli var: "son bir oyun kaldı" ve "bugünlük bu kadar". İkisi de
    /// tur <b>bittikten sonra</b> söyleniyor. Oyuna girerken söylemek çocuğu
    /// acele ettirirdi; ekranda geri sayan bir saat ise bu yaşta doğrudan
    /// kaygı üretiyor.
    /// </remarks>
    [ObservableProperty]
    public partial string ScreenTimeText { get; set; } = string.Empty;

    [ObservableProperty]
    public partial bool HasScreenTimeNotice { get; set; }

    /// <summary>
    /// Bu turla oyun bir kademe zorlaştı.
    /// </summary>
    /// <remarks>
    /// Uyarlamanın görünür olduğu ilk yer burası ve tek söylendiği an bu.
    /// Ana ekrandaki işaret kalıcı, bu cümle ise <b>olayın kendisini</b>
    /// duyuruyor: sonraki tur neden zorlaştı sorusu, sorulmadan cevaplanıyor.
    /// </remarks>
    [ObservableProperty]
    public partial bool HasStretchNotice { get; set; }

    [ObservableProperty]
    public partial string StretchText { get; set; } = string.Empty;

    /// <summary>
    /// Bugünlük bitti — "tekrar oyna" gizleniyor.
    /// </summary>
    /// <remarks>
    /// Düğmeyi bırakıp dokunulduğunda reddetmek, çocuğa cezalandırılmış gibi
    /// hissettiriyor. Yok olması "bugün burada bitti"yi tartışmasız yapıyor.
    /// </remarks>
    [ObservableProperty]
    public partial bool CanPlayAgain { get; set; } = true;

    public ObservableCollection<PlayerResult> Players { get; } = [];

    public void Load()
    {
        var summary = flow.LastSummary;
        if (summary is null)
        {
            return;
        }

        var l = LocalizationService.Instance;

        GameName = GamePresentation.Name(summary.GameId);
        IsMultiplayer = summary.IsMultiplayer;

        Players.Clear();
        foreach (var player in summary.Players)
        {
            Players.Add(player);
        }

        if (!summary.IsMultiplayer)
        {
            SoloStars = summary.Players.Count > 0 ? summary.Players[0].Stars : 0;
            Headline = l["RoundCompleteTitle"];
            return;
        }

        Headline = summary.IsDraw
            ? l["EveryoneWins"]
            : l.Format("WinnerIs", summary.Winners[0].DisplayName);
    }

    /// <summary>
    /// Bu turla açılan avatarları bulur ve kutlama şeridini doldurur.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Kaynak, kayıtlı <b>ödül işareti</b>: en son hangi yıldız sayısında
    /// kutlama yapıldığı. Karşılaştırma turun kendi yıldızıyla değil bu
    /// işaretle yapılıyor, çünkü tur bir eşiği tam ortasından atlayabiliyor
    /// ve uygulama kutlama anında kapanabiliyor. İşaret ileri alındığı için
    /// aynı ödül ikinci kez kutlanmıyor, atlanan da kaybolmuyor.
    /// </para>
    /// <para>
    /// Kutlama <b>o an seçili çocuk</b> için yapılıyor. Sıralı oyunda
    /// kardeşin kazandığı ödül burada görünmüyor ama kaybolmuyor: onun
    /// işareti yerinde duruyor ve kendi sırası geldiğinde kutlanıyor.
    /// Sonuç ekranında iki çocuğun ödülünü aynı anda göstermek, ekranı
    /// asıl işinden — kimin ne yaptığından — uzaklaştırıyordu.
    /// </para>
    /// </remarks>
    public async Task LoadRewardsAsync()
    {
        UnlockedAvatars.Clear();
        HasUnlock = false;

        var profile = state.ActiveProfile;
        if (profile is null)
        {
            return;
        }

        await LoadScreenTimeAsync(profile.Id);
        await RefreshStepsAsync(profile.Id);

        var total = await repository.TotalStarsAsync(profile.Id);
        var seen = await repository.RewardWatermarkAsync(profile.Id, total);

        foreach (var avatar in AvatarCatalog.UnlockedBetween(seen, total))
        {
            UnlockedAvatars.Add(avatar);
        }

        await repository.SetRewardWatermarkAsync(profile.Id, total);

        if (UnlockedAvatars.Count == 0)
        {
            return;
        }

        HasUnlock = true;
        UnlockText = LocalizationService.Instance[
            UnlockedAvatars.Count == 1 ? "RewardUnlockedOne" : "RewardUnlockedMany"];
    }

    /// <summary>
    /// Oyun süresi bütçesini okur ve haberi hazırlar.
    /// </summary>
    /// <remarks>
    /// Sıralı oyunda da o an seçili çocuğun bütçesine bakılıyor: bütçe profil
    /// başına ve kardeşin süresi kardeşin sırası geldiğinde ölçülüyor.
    /// </remarks>
    private async Task LoadScreenTimeAsync(int profileId)
    {
        var status = await repository.ScreenTimeTodayAsync(profileId);
        var l = LocalizationService.Instance;

        CanPlayAgain = !status.IsSpent;
        HasScreenTimeNotice = status.IsSpent || status.IsLastRound;

        ScreenTimeText = status.IsSpent
            ? l["ScreenTimeDone"]
            : status.IsLastRound
                ? l["ScreenTimeLastRound"]
                : string.Empty;
    }

    /// <summary>
    /// Oturumun kademelerini bu turun sonucuna göre tazeler ve kademe
    /// atlandıysa haberi hazırlar.
    /// </summary>
    /// <remarks>
    /// Tazeleme şart: "tekrar oyna" bekleyen oturuma dönüyor, yani kademe
    /// yenilenmeseydi üst üste oynayan çocuk ana ekrana dönene kadar hiç
    /// yukarı çıkamazdı — tam da en çok oynayan çocuk.
    ///
    /// Haber yalnızca <b>o an seçili</b> çocuk için: sıralı oyunda kardeşin
    /// kademesi de tazeleniyor ama duyurusu, kendi sırası geldiğinde
    /// yapılıyor. Ödül kutlaması da aynı sebeple böyle.
    /// </remarks>
    private async Task RefreshStepsAsync(int activeProfileId)
    {
        HasStretchNotice = false;
        StretchText = string.Empty;

        if (flow.PendingSession is not { } session)
        {
            return;
        }

        var players = new List<Player>(session.Players.Count);
        var promoted = false;

        foreach (var played in session.Players)
        {
            var step = await repository.StepForAsync(
                played.ProfileId, session.GameId, played.Band);

            players.Add(played with { Step = step });

            promoted |= played.ProfileId == activeProfileId
                && played.Step == DifficultyStep.Base
                && step == DifficultyStep.Stretch;
        }

        flow.PendingSession = new GameSession(
            session.GameId, session.Mode, players, session.RoundsPerPlayer);

        if (!promoted)
        {
            return;
        }

        HasStretchNotice = true;
        StretchText = LocalizationService.Instance.Format(
            "GameStretchedNotice", GamePresentation.Name(session.GameId));
    }

    /// <summary>Kutlama şeridine dokununca koleksiyon açılıyor.</summary>
    [RelayCommand]
    private static async Task OpenCollectionAsync() =>
        await Shell.Current.GoToAsync("collection");

    [RelayCommand]
    private static async Task PlayAgainAsync()
    {
        // Oyun sayfası geri yığınında duruyor; oraya dönmek yeni bir oturum
        // başlatıyor (sayfa her görünüşte kendini kuruyor).
        await Shell.Current.GoToAsync("..");
    }

    [RelayCommand]
    private async Task BackToGamesAsync()
    {
        flow.Clear();
        await Navigation.GoHomeAsync();
    }
}
