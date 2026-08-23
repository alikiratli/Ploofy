using Ploofy.Engine;
using Ploofy.Engine.Progress;

namespace Ploofy.Engine.Tests;

public class StarRatingTests
{
    private static RoundOutcome Outcome(
        AgeBand band,
        bool completed = true,
        int correct = 6,
        int mistakes = 0,
        int elapsedSeconds = 30,
        int? parSeconds = null) =>
        new(
            GameId: "memory_match",
            ProfileId: 1,
            Band: band,
            Completed: completed,
            Correct: correct,
            Mistakes: mistakes,
            Elapsed: TimeSpan.FromSeconds(elapsedSeconds),
            ParTime: parSeconds is null ? null : TimeSpan.FromSeconds(parSeconds.Value));

    [Fact]
    public void Filiz_completing_is_always_three_stars()
    {
        // Bu bantta kaybetme yok: hata da süre de yıldızı etkilemez.
        Assert.Equal(3, StarRating.ForOutcome(Outcome(AgeBand.Filiz, mistakes: 12)));
        Assert.Equal(3, StarRating.ForOutcome(Outcome(AgeBand.Filiz, elapsedSeconds: 600)));
    }

    [Fact]
    public void Filiz_still_earns_a_star_when_the_round_is_abandoned()
    {
        // Denemiş olmanın karşılığı bu yaşta ödülsüz kalmamalı.
        Assert.Equal(1, StarRating.ForOutcome(Outcome(AgeBand.Filiz, completed: false)));
    }

    [Fact]
    public void Older_bands_earn_nothing_for_an_abandoned_round()
    {
        Assert.Equal(0, StarRating.ForOutcome(Outcome(AgeBand.Fidan, completed: false)));
        Assert.Equal(0, StarRating.ForOutcome(Outcome(AgeBand.Mese, completed: false)));
    }

    [Theory]
    [InlineData(0, 3)]
    [InlineData(1, 3)]
    [InlineData(2, 2)]
    [InlineData(3, 2)]
    [InlineData(4, 1)]
    public void Fidan_forgives_a_couple_of_mistakes(int mistakes, int expectedStars) =>
        Assert.Equal(
            expectedStars,
            StarRating.ForOutcome(Outcome(AgeBand.Fidan, mistakes: mistakes)));

    [Fact]
    public void Mese_needs_a_flawless_round_within_par_for_three_stars()
    {
        Assert.Equal(
            3,
            StarRating.ForOutcome(Outcome(AgeBand.Mese, elapsedSeconds: 60, parSeconds: 75)));

        // Hatasız ama yavaş: üçüncü yıldız süreye takılıyor.
        Assert.Equal(
            2,
            StarRating.ForOutcome(Outcome(AgeBand.Mese, elapsedSeconds: 90, parSeconds: 75)));
    }

    [Fact]
    public void Mese_without_a_par_time_only_needs_a_flawless_round()
    {
        Assert.Equal(
            3,
            StarRating.ForOutcome(Outcome(AgeBand.Mese, elapsedSeconds: 999, parSeconds: null)));
    }

    [Fact]
    public void Mese_drops_to_one_star_once_accuracy_falls_below_three_quarters()
    {
        // 6 doğru / 6 hata = %50 isabet.
        Assert.Equal(
            1,
            StarRating.ForOutcome(Outcome(AgeBand.Mese, correct: 6, mistakes: 6)));

        // 9 doğru / 3 hata = %75 isabet, sınırda iki yıldız.
        Assert.Equal(
            2,
            StarRating.ForOutcome(Outcome(AgeBand.Mese, correct: 9, mistakes: 3)));
    }

    [Fact]
    public void Raw_score_is_separate_from_stars_so_bands_stay_comparable()
    {
        // Filiz her turda üç yıldız alıyor; sıralamada bu ona üstünlük vermemeli.
        var filiz = Outcome(AgeBand.Filiz, correct: 3, mistakes: 0);
        var mese = Outcome(AgeBand.Mese, correct: 10, mistakes: 1);

        Assert.Equal(3, StarRating.ForOutcome(filiz));
        Assert.True(StarRating.RawScore(mese) > StarRating.RawScore(filiz));
    }

    [Fact]
    public void Raw_score_never_goes_negative()
    {
        var disaster = Outcome(AgeBand.Mese, correct: 0, mistakes: 20);
        Assert.Equal(0, StarRating.RawScore(disaster));
    }
}
