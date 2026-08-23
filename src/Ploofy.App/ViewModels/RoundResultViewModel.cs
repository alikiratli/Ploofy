using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;

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
public sealed partial class RoundResultViewModel(PlayFlow flow) : ObservableObject
{
    [ObservableProperty]
    public partial string GameName { get; set; }

    [ObservableProperty]
    public partial string Headline { get; set; }

    [ObservableProperty]
    public partial bool IsMultiplayer { get; set; }

    [ObservableProperty]
    public partial int SoloStars { get; set; }

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
