using Ploofy.Engine;
using Ploofy.Engine.Catalog;
using Ploofy.Engine.Progress;

namespace Ploofy.Engine.Tests;

/// <summary>
/// Grafiğin çubuğu doğru yükseklikte mi, ekrana bakarak anlaşılmıyor. Hesap
/// burada sınanıyor.
/// </summary>
public class PlayReportTests
{
    private static readonly DateOnly Today = new(2026, 9, 2);

    private static PlayedRound Round(
        int daysAgo,
        string gameId = GameCatalog.MemoryMatch,
        int stars = 2,
        double minutes = 3,
        int mistakes = 0) =>
        new(
            Today.AddDays(-daysAgo),
            gameId,
            AgeBand.Fidan,
            stars,
            mistakes,
            TimeSpan.FromMinutes(minutes));

    [Fact]
    public void The_report_has_one_row_per_day_even_when_nothing_was_played()
    {
        // Boş günün yerini boş bırakmak, hafta sonu oynanmadığını gösteren
        // tek şey. Atlanan gün çubukları yan yana getirip eğilimi olduğundan
        // düzgün gösteriyor.
        var report = PlayReport.Build([Round(0), Round(6)], Today, days: 7);

        Assert.Equal(7, report.Days.Count);
        Assert.Equal(Today.AddDays(-6), report.From);
        Assert.Equal(Today, report.To);
        Assert.Equal(2, report.ActiveDays);
        Assert.Equal(5, report.Days.Count(d => d.Rounds == 0));
    }

    [Fact]
    public void The_days_run_from_oldest_to_newest()
    {
        // Grafik soldan sağa okunuyor; ters sıra eğilimi tam tersine
        // gösterirdi ve bunu ekranda fark etmek zor.
        var report = PlayReport.Build([], Today, days: 14);

        Assert.Equal(14, report.Days.Count);
        Assert.Equal(report.Days.OrderBy(d => d.Date), report.Days);
        Assert.Equal(Today, report.Days[^1].Date);
    }

    [Fact]
    public void Rounds_outside_the_window_are_dropped()
    {
        var report = PlayReport.Build([Round(0), Round(30)], Today, days: 7);

        Assert.Equal(1, report.TotalRounds);
    }

    [Fact]
    public void A_day_adds_up_its_rounds_stars_and_minutes()
    {
        var report = PlayReport.Build(
            [
                Round(1, stars: 3, minutes: 4),
                Round(1, stars: 1, minutes: 2),
                Round(0, stars: 2, minutes: 5),
            ],
            Today,
            days: 3);

        var yesterday = report.Days.Single(d => d.Date == Today.AddDays(-1));

        Assert.Equal(2, yesterday.Rounds);
        Assert.Equal(4, yesterday.Stars);
        Assert.Equal(TimeSpan.FromMinutes(6), yesterday.Duration);

        Assert.Equal(3, report.TotalRounds);
        Assert.Equal(6, report.TotalStars);
        Assert.Equal(TimeSpan.FromMinutes(11), report.TotalDuration);
    }

    [Fact]
    public void A_forgotten_round_cannot_swallow_the_whole_report()
    {
        // Cihazı bırakıp akşam dönen çocuk, kırpma olmadan "bugün 6 saat
        // oynadı" satırı üretiyor ve o tek satır bütün raporu yalancı yapıyor.
        var report = PlayReport.Build([Round(0, minutes: 360)], Today, days: 7);

        Assert.Equal(PlayReport.LongestCountedRound, report.TotalDuration);
    }

    [Fact]
    public void A_clock_that_went_backwards_does_not_produce_negative_time()
    {
        var round = Round(0) with { Duration = TimeSpan.FromMinutes(-5) };
        var report = PlayReport.Build([round], Today, days: 7);

        Assert.Equal(TimeSpan.Zero, report.TotalDuration);
        Assert.Equal(1, report.TotalRounds);
    }

    [Fact]
    public void The_busiest_day_sets_the_scale_of_the_chart()
    {
        var report = PlayReport.Build(
            [Round(2, minutes: 3), Round(1, minutes: 9), Round(0, minutes: 4)],
            Today,
            days: 5);

        Assert.Equal(TimeSpan.FromMinutes(9), report.BusiestDay);
    }

    [Fact]
    public void An_empty_report_says_so_instead_of_dividing_by_zero()
    {
        var report = PlayReport.Build([], Today, days: 14);

        Assert.True(report.IsEmpty);
        Assert.Equal(0, report.ActiveDays);
        Assert.Equal(TimeSpan.Zero, report.BusiestDay);
        Assert.Equal(TimeSpan.Zero, report.TotalDuration);
        Assert.Empty(report.Games);
    }

    [Fact]
    public void Games_are_listed_most_played_first()
    {
        var report = PlayReport.Build(
            [
                Round(0, GameCatalog.BubblePop),
                Round(1, GameCatalog.BubblePop),
                Round(2, GameCatalog.BubblePop),
                Round(0, GameCatalog.Jigsaw),
                Round(1, GameCatalog.Jigsaw),
                Round(3, GameCatalog.Pattern),
            ],
            Today,
            days: 7);

        Assert.Equal(
            [GameCatalog.BubblePop, GameCatalog.Jigsaw, GameCatalog.Pattern],
            report.Games.Select(g => g.GameId));
    }

    [Fact]
    public void A_game_row_keeps_the_best_star_and_the_last_day_it_was_played()
    {
        // Ebeveynin sorduğu iki şey: en iyi ne yaptı, en son ne zaman oynadı.
        var report = PlayReport.Build(
            [
                Round(5, GameCatalog.Jigsaw, stars: 1, minutes: 2),
                Round(1, GameCatalog.Jigsaw, stars: 3, minutes: 4),
                Round(3, GameCatalog.Jigsaw, stars: 2, minutes: 1),
            ],
            Today,
            days: 7);

        var jigsaw = report.Games.Single();

        Assert.Equal(3, jigsaw.Rounds);
        Assert.Equal(3, jigsaw.BestStars);
        Assert.Equal(6, jigsaw.Stars);
        Assert.Equal(TimeSpan.FromMinutes(7), jigsaw.Duration);
        Assert.Equal(Today.AddDays(-1), jigsaw.LastPlayedOn);
    }

    [Fact]
    public void A_game_row_clamps_its_minutes_the_same_way_the_day_does()
    {
        // İki yerde ayrı hesaplanıyorlar; biri kırpar diğeri kırpmazsa oyun
        // satırlarının toplamı gün satırlarının toplamını tutmuyor.
        var report = PlayReport.Build([Round(0, minutes: 360)], Today, days: 7);

        Assert.Equal(report.TotalDuration, report.Games.Single().Duration);
    }

    [Fact]
    public void A_single_day_report_is_allowed()
    {
        var report = PlayReport.Build([Round(0)], Today, days: 1);

        Assert.Single(report.Days);
        Assert.Equal(Today, report.From);
        Assert.Equal(1, report.TotalRounds);
    }

    [Fact]
    public void A_report_of_no_days_is_a_mistake()
    {
        Assert.Throws<ArgumentOutOfRangeException>(
            () => PlayReport.Build([], Today, days: 0));
    }
}
