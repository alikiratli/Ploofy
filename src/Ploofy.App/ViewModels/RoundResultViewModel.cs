using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;

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
