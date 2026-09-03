using Ploofy.Engine;
using Ploofy.Engine.Catalog;
using Ploofy.Engine.Progress;

namespace Ploofy.Engine.Tests;

/// <summary>
/// Günlük oyun süresi bütçesi. Yanlışı ancak çocuk haksız yere kilitlendiğinde
/// görülürdü, o yüzden hesap burada sınanıyor.
/// </summary>
public class ScreenTimeBudgetTests
{
    private static readonly DateOnly Today = new(2026, 9, 3);

    private static PlayedRound Round(double minutes, int daysAgo = 0) => new(
        Today.AddDays(-daysAgo),
        GameCatalog.MemoryMatch,
        AgeBand.Fidan,
        Stars: 2,
        Mistakes: 0,
        Duration: TimeSpan.FromMinutes(minutes));

    [Fact]
    public void No_limit_means_no_limit()
    {
        // Varsayılan bu. Açık gelseydi güncellemeden sonra bütün çocuklar
        // birden kilitlenirdi.
        var status = ScreenTimeBudget.Evaluate(
            ScreenTimeBudget.Unlimited, [Round(90)], Today);

        Assert.True(status.IsUnlimited);
        Assert.False(status.IsSpent);
        Assert.False(status.IsLastRound);
        Assert.Equal(0d, status.Fraction);
    }

    [Fact]
    public void A_negative_limit_is_treated_as_no_limit()
    {
        var status = ScreenTimeBudget.Evaluate(-5, [Round(90)], Today);

        Assert.True(status.IsUnlimited);
        Assert.False(status.IsSpent);
    }

    [Fact]
    public void Only_todays_rounds_count()
    {
        // Bütçe her gece kendiliğinden doluyor; dün oynanan bugünü yemiyor.
        var status = ScreenTimeBudget.Evaluate(
            20, [Round(18, daysAgo: 1), Round(4)], Today);

        Assert.Equal(TimeSpan.FromMinutes(4), status.Used);
        Assert.False(status.IsSpent);
    }

    [Fact]
    public void The_budget_runs_out_when_the_limit_is_reached()
    {
        var status = ScreenTimeBudget.Evaluate(20, [Round(12), Round(8)], Today);

        Assert.True(status.IsSpent);
        Assert.Equal(TimeSpan.Zero, status.Remaining);
        Assert.Equal(1d, status.Fraction);
    }

    [Fact]
    public void Going_over_the_limit_does_not_produce_negative_time()
    {
        // Tur ortasında kesilmediği için bütçe her zaman biraz aşılıyor.
        var status = ScreenTimeBudget.Evaluate(20, [Round(14), Round(12)], Today);

        Assert.True(status.IsSpent);
        Assert.Equal(TimeSpan.Zero, status.Remaining);
        Assert.Equal(1d, status.Fraction);
    }

    [Fact]
    public void A_long_forgotten_round_cannot_eat_the_whole_day()
    {
        // Kronometre uygulama arka plandayken durmuyor. Kırpma olmasaydı
        // bırakılmış tek bir tur çocuğun bütün gününü yakardı.
        var status = ScreenTimeBudget.Evaluate(60, [Round(360)], Today);

        Assert.Equal(PlayReport.LongestCountedRound, status.Used);
        Assert.False(status.IsSpent);
    }

    [Fact]
    public void The_last_round_warning_arrives_before_the_budget_ends()
    {
        var status = ScreenTimeBudget.Evaluate(20, [Round(12), Round(4)], Today);

        Assert.False(status.IsSpent);
        Assert.True(status.IsLastRound);
        Assert.Equal(TimeSpan.FromMinutes(4), status.Remaining);
    }

    [Fact]
    public void There_is_no_warning_while_there_is_still_plenty_of_time()
    {
        var status = ScreenTimeBudget.Evaluate(20, [Round(10)], Today);

        Assert.False(status.IsLastRound);
        Assert.False(status.IsSpent);
    }

    [Fact]
    public void A_spent_budget_is_not_also_a_last_round()
    {
        // İkisi aynı anda doğru olsaydı ekran hem "son oyun" hem "bugünlük
        // bu kadar" derdi.
        var status = ScreenTimeBudget.Evaluate(20, [Round(12), Round(8)], Today);

        Assert.True(status.IsSpent);
        Assert.False(status.IsLastRound);
    }

    [Fact]
    public void The_smallest_limit_does_not_start_out_as_a_last_round()
    {
        // Uyarı eşiği en küçük seçenekten kısa olmalı. Olmasaydı, on
        // dakikalık sınırı seçen ebeveynin çocuğu daha hiç oynamadan
        // "son oyun" uyarısıyla karşılaşırdı.
        var smallest = ScreenTimeBudget.Choices.Min();
        var status = ScreenTimeBudget.Evaluate(smallest, [], Today);

        Assert.False(status.IsLastRound);
        Assert.False(status.IsSpent);
    }

    [Fact]
    public void A_forgotten_round_is_capped_but_still_counted()
    {
        // Kırpma sınırı, turların gerçek uzunluğu değil: uyarı eşiğiyle
        // karıştırılmamalı. On beş dakikalık tek bir tur, yirmi dakikalık
        // bütçeyi bitirmiyor ama "son oyun"a düşürüyor.
        var status = ScreenTimeBudget.Evaluate(20, [Round(600)], Today);

        Assert.Equal(PlayReport.LongestCountedRound, status.Used);
        Assert.False(status.IsSpent);
        Assert.True(status.IsLastRound);
    }

    [Fact]
    public void A_day_with_no_rounds_uses_nothing()
    {
        var status = ScreenTimeBudget.Evaluate(20, [], Today);

        Assert.Equal(TimeSpan.Zero, status.Used);
        Assert.Equal(TimeSpan.FromMinutes(20), status.Remaining);
        Assert.False(status.IsSpent);
    }

    [Fact]
    public void The_suggested_limit_grows_with_the_band()
    {
        // Küçük bantta dikkat süresi de turlar da kısa; Meşe'nin tek bir turu
        // birkaç dakika sürüyor.
        var filiz = ScreenTimeBudget.SuggestedMinutes.For(AgeBand.Filiz);
        var fidan = ScreenTimeBudget.SuggestedMinutes.For(AgeBand.Fidan);
        var mese = ScreenTimeBudget.SuggestedMinutes.For(AgeBand.Mese);

        Assert.True(filiz < fidan);
        Assert.True(fidan < mese);
        Assert.All(
            new[] { filiz, fidan, mese },
            m => Assert.Contains(m, ScreenTimeBudget.Choices));
    }
}
