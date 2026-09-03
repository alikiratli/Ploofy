using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Engine;
using Ploofy.Engine.Progress;

namespace Ploofy.App.ViewModels;

/// <summary>Rapor ekranındaki bir çocuk sekmesi.</summary>
public sealed partial class ReportProfile : ObservableObject
{
    public required int Id { get; init; }

    public required string DisplayName { get; init; }

    public required string AvatarId { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>Rapordaki bir oyun satırı.</summary>
public sealed record ReportGameRow(
    string Name,
    string Glyph,
    string Rounds,
    string Duration,
    string BestStars,
    string LastPlayed);

/// <summary>Dönem seçeneği (7 / 14 / 30 gün).</summary>
public sealed partial class ReportRange : ObservableObject
{
    public required int Days { get; init; }

    public required string Label { get; init; }

    [ObservableProperty]
    public partial bool IsSelected { get; set; }
}

/// <summary>
/// Ebeveyn raporu.
/// </summary>
/// <remarks>
/// <para>
/// Ücretli bir çocuk uygulamasında ebeveynin karşılığını gördüğü yer burası.
/// Hesabın tamamı <see cref="PlayReport"/> içinde ve testli; buradaki iş
/// yalnızca satırları okuyup biçimlendirmek.
/// </para>
/// <para>
/// Ekran ebeveyn kilidinin arkasındaki ayarlardan açılıyor ve gösterdiği her
/// şey bu cihazdan geliyor — hiçbir sorgu dışarı çıkmıyor. Ekranın altındaki
/// cümle bunu ebeveyne de söylüyor.
/// </para>
/// </remarks>
public sealed partial class ReportViewModel(ProgressRepository repository) : ObservableObject
{
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }

    [ObservableProperty]
    public partial string TimePlayed { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Rounds { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string ActiveDays { get; set; } = string.Empty;

    [ObservableProperty]
    public partial string Stars { get; set; } = string.Empty;

    /// <summary>Grafiğin okuduğu günler.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<ReportDay> Days { get; set; } = [];

    /// <summary>Gün harfleri, Pazar'dan Cumartesi'ye.</summary>
    [ObservableProperty]
    public partial IReadOnlyList<string> WeekdayInitials { get; set; } = [];

    [ObservableProperty]
    public partial string MinuteSuffix { get; set; } = string.Empty;

    public ObservableCollection<ReportProfile> Profiles { get; } = [];

    public ObservableCollection<ReportRange> Ranges { get; } = [];

    public ObservableCollection<ReportGameRow> Games { get; } = [];

    /// <summary>Birden çok çocuk yoksa sekme şeridi gösterilmiyor.</summary>
    public bool ShowsProfilePicker => Profiles.Count > 1;

    public async Task LoadAsync()
    {
        var l = LocalizationService.Instance;

        // Gün adları kültürden ve **kısaltılmış** biçimde. Tek harf olmaz:
        // Türkçe'de Pazar, Pazartesi ve Perşembe'nin üçü de "P" — kültürün
        // ShortestDayNames'i tam olarak bunu veriyor ve ayırt etmiyor.
        // Kısaltılmış ad (Paz/Pzt/Sal/Çar/Per/Cum/Cmt) okunuyor; dar dönemde
        // grafiğin kendisi etiketleri seyreltiyor.
        WeekdayInitials = l.Culture.DateTimeFormat.AbbreviatedDayNames;
        MinuteSuffix = l["ReportMinuteShort"];

        if (Ranges.Count == 0)
        {
            Ranges.Add(new ReportRange { Days = 7, Label = l["ReportRange7"] });
            Ranges.Add(new ReportRange { Days = 14, Label = l["ReportRange14"], IsSelected = true });
            Ranges.Add(new ReportRange { Days = 30, Label = l["ReportRange30"] });
        }

        if (Profiles.Count == 0)
        {
            foreach (var row in await repository.ListProfilesAsync())
            {
                Profiles.Add(new ReportProfile
                {
                    Id = row.Id,
                    DisplayName = row.DisplayName,
                    AvatarId = row.AvatarId,
                    IsSelected = Profiles.Count == 0,
                });
            }

            OnPropertyChanged(nameof(ShowsProfilePicker));
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private async Task SelectProfileAsync(ReportProfile? profile)
    {
        if (profile is null)
        {
            return;
        }

        foreach (var row in Profiles)
        {
            row.IsSelected = row.Id == profile.Id;
        }

        await RefreshAsync();
    }

    [RelayCommand]
    private async Task SelectRangeAsync(ReportRange? range)
    {
        if (range is null)
        {
            return;
        }

        foreach (var row in Ranges)
        {
            row.IsSelected = row.Days == range.Days;
        }

        await RefreshAsync();
    }

    private async Task RefreshAsync()
    {
        var profile = Profiles.FirstOrDefault(p => p.IsSelected);
        var range = Ranges.FirstOrDefault(r => r.IsSelected) ?? Ranges.FirstOrDefault();

        if (profile is null || range is null)
        {
            Show(PlayReport.Build([], DateOnly.FromDateTime(DateTime.Now), 14));
            return;
        }

        var today = DateOnly.FromDateTime(DateTime.Now);
        var since = today.AddDays(-(range.Days - 1));

        var history = await repository.HistorySinceAsync(profile.Id, since);

        var rounds = history.Select(ProgressRepository.ToPlayedRound).ToList();

        Show(PlayReport.Build(rounds, today, range.Days));
    }

    private void Show(PlayReport report)
    {
        var l = LocalizationService.Instance;

        Days = report.Days;
        IsEmpty = report.IsEmpty;

        TimePlayed = FormatDuration(report.TotalDuration);
        Rounds = report.TotalRounds.ToString(l.Culture);
        ActiveDays = report.ActiveDays.ToString(l.Culture);
        Stars = report.TotalStars.ToString(l.Culture);

        Games.Clear();
        foreach (var game in report.Games)
        {
            Games.Add(new ReportGameRow(
                GamePresentation.Name(game.GameId),
                GamePresentation.Glyph(game.GameId),
                l.Format("ReportGameRounds", game.Rounds),
                FormatDuration(game.Duration),
                l.Format("ReportBestStars", game.BestStars),
                game.LastPlayedOn.ToString("d MMMM", l.Culture)));
        }
    }

    /// <summary>
    /// Süreyi ebeveynin okuyacağı gibi yazar.
    /// </summary>
    /// <remarks>
    /// Bir saatin altında yalnızca dakika: "0 sa 24 dk" ebeveyne hiçbir şey
    /// katmıyor ve satırı uzatıyor. Dakika her zaman yukarı yuvarlanıyor —
    /// oynanmış bir dönemi "0 dk" diye göstermek raporu yalancı yapar.
    /// </remarks>
    private static string FormatDuration(TimeSpan duration)
    {
        var l = LocalizationService.Instance;

        if (duration <= TimeSpan.Zero)
        {
            return l.Format("ReportMinutes", 0);
        }

        var minutes = Math.Max(1, (int)Math.Ceiling(duration.TotalMinutes));

        return minutes < 60
            ? l.Format("ReportMinutes", minutes)
            : l.Format("ReportHoursMinutes", minutes / 60, minutes % 60);
    }

    [RelayCommand]
    private static async Task CloseAsync() => await Shell.Current.GoToAsync("..");
}
