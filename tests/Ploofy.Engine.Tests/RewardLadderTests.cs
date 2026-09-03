using Ploofy.Engine.Progress;

namespace Ploofy.Engine.Tests;

/// <summary>
/// Ödül merdiveni: toplam yıldız kaç avatar açıyor.
/// </summary>
/// <remarks>
/// Ekrana bakarak sınanamayacak bir hesap, çünkü yanlışı ancak altmış yıldız
/// biriktiren bir çocuk görürdü.
/// </remarks>
public class RewardLadderTests
{
    /// <summary>Kataloğun kilitlenebilir avatar sayısı.</summary>
    private const int Rewards = 20;

    [Fact]
    public void The_first_reward_arrives_after_one_perfect_round()
    {
        // Üç yıldız bir turdan alınabilecek en yüksek puan. İlk ödülün orada
        // durması, kuralın anlatılmadan görülmesini sağlayan şey.
        Assert.Equal(0, RewardLadder.UnlockedCount(2, Rewards));
        Assert.Equal(1, RewardLadder.UnlockedCount(3, Rewards));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(3, 1)]
    [InlineData(5, 1)]
    [InlineData(6, 2)]
    [InlineData(29, 9)]
    [InlineData(30, 10)]
    public void The_unlocked_count_is_the_star_total_divided_by_the_step(
        int stars, int expected) =>
        Assert.Equal(expected, RewardLadder.UnlockedCount(stars, Rewards));

    [Fact]
    public void The_count_never_passes_the_number_of_rewards()
    {
        // Bantlar arası toplam yıldız yüzü geçebiliyor; merdiven bittiğinde
        // yirmi birinci avatar diye bir şey yok.
        Assert.Equal(Rewards, RewardLadder.UnlockedCount(1000, Rewards));
        Assert.Equal(0, RewardLadder.UnlockedCount(1000, 0));
    }

    [Fact]
    public void A_negative_total_is_treated_as_zero()
    {
        var progress = RewardLadder.Evaluate(-5, Rewards);

        Assert.Equal(0, progress.TotalStars);
        Assert.Equal(0, progress.Unlocked);
        Assert.Equal(3, progress.NextRequiredStars);
    }

    [Fact]
    public void The_next_threshold_is_the_step_after_the_last_unlock()
    {
        var progress = RewardLadder.Evaluate(7, Rewards);

        Assert.Equal(2, progress.Unlocked);
        Assert.Equal(9, progress.NextRequiredStars);
        Assert.Equal(2, progress.StarsToNext);
        Assert.False(progress.IsComplete);
    }

    [Fact]
    public void A_finished_ladder_has_no_next_threshold()
    {
        var progress = RewardLadder.Evaluate(60, Rewards);

        Assert.True(progress.IsComplete);
        Assert.Null(progress.NextRequiredStars);
        Assert.Equal(0, progress.StarsToNext);
        Assert.Equal(1.0, progress.FractionToNext);
    }

    [Fact]
    public void The_bar_measures_between_two_thresholds_not_from_zero()
    {
        // Sıfırdan ölçülseydi bu çubuk %78 dolu görünürdü ve çocuk iki
        // yıldız daha kazandığında hiç ilerlemediğini sanırdı.
        var progress = RewardLadder.Evaluate(7, Rewards);

        Assert.Equal(1.0 / 3.0, progress.FractionToNext, 5);
    }

    [Fact]
    public void The_bar_is_empty_right_after_a_reward_lands()
    {
        Assert.Equal(0.0, RewardLadder.Evaluate(3, Rewards).FractionToNext, 5);
        Assert.Equal(0.0, RewardLadder.Evaluate(0, Rewards).FractionToNext, 5);
    }
}
