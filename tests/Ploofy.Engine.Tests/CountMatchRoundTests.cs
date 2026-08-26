using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

public class CountMatchRoundTests
{
    private static CountMatchRound Round(AgeBand band, int seed = 7) =>
        CountMatchRound.ForBand(band, new Random(seed));

    /// <summary>Turu baştan sona doğru oynar ve sorulan miktarları döner.</summary>
    private static List<int> PlayThrough(CountMatchRound round)
    {
        var counts = new List<int>();

        while (round.Current is { } question)
        {
            counts.Add(question.Group.Count);
            Assert.Equal(CountOutcome.Correct, round.Drop(question.Group.Count));
        }

        return counts;
    }

    [Theory]
    [InlineData(AgeBand.Filiz, 4, 2)]
    [InlineData(AgeBand.Fidan, 6, 3)]
    [InlineData(AgeBand.Mese, 8, 4)]
    public void Questions_and_choices_scale_with_the_band(AgeBand band, int questions, int choices)
    {
        var round = Round(band);

        Assert.Equal(questions, round.Total);
        Assert.Equal(choices, round.Current!.Choices.Count);
    }

    [Theory]
    [InlineData(AgeBand.Filiz, 3)]
    [InlineData(AgeBand.Fidan, 5)]
    [InlineData(AgeBand.Mese, 10)]
    public void The_amount_never_leaves_the_bands_range(AgeBand band, int max)
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var round = CountMatchRound.ForBand(band, new Random(seed));

            while (round.Current is { } question)
            {
                Assert.InRange(question.Group.Count, 1, max);
                Assert.All(question.Choices, choice => Assert.InRange(choice, 1, max));
                round.Drop(question.Group.Count);
            }
        }
    }

    [Fact]
    public void The_right_answer_is_always_on_screen()
    {
        var round = Round(AgeBand.Mese);

        while (round.Current is { } question)
        {
            Assert.Contains(question.Group.Count, question.Choices);
            round.Drop(question.Group.Count);
        }
    }

    [Fact]
    public void Choices_are_distinct_and_read_left_to_right()
    {
        // Tepsi küçük bir sayı doğrusu gibi okunuyor: "daha çok olan daha sağda".
        for (var seed = 0; seed < 40; seed++)
        {
            var round = CountMatchRound.ForBand(AgeBand.Mese, new Random(seed));

            while (round.Current is { } question)
            {
                Assert.Equal(question.Choices.Count, question.Choices.Distinct().Count());
                Assert.Equal(question.Choices.OrderBy(c => c), question.Choices);
                round.Drop(question.Group.Count);
            }
        }
    }

    [Fact]
    public void The_oldest_band_has_to_count_instead_of_guessing()
    {
        // Meşe'de çeldiriciler elde olan en yakın sayılar: 6 ile 7 arasından
        // seçmek gerçekten saymayı gerektiriyor. Aralığın ucunda pencere
        // genişliyor ama seçilen hâlâ ekrandaki en yakınlar olmalı.
        for (var seed = 0; seed < 40; seed++)
        {
            var round = CountMatchRound.ForBand(AgeBand.Mese, new Random(seed));

            while (round.Current is { } question)
            {
                var target = question.Group.Count;
                var chosen = question.Choices.Where(c => c != target).ToList();
                var skipped = Enumerable
                    .Range(1, 10)
                    .Where(n => n != target && !chosen.Contains(n));

                var farthestChosen = chosen.Max(c => Math.Abs(c - target));

                Assert.All(skipped, n => Assert.True(
                    Math.Abs(n - target) >= farthestChosen,
                    $"{target} için {n} varken daha uzak bir çeldirici seçilmiş"));

                round.Drop(target);
            }
        }
    }

    [Fact]
    public void Younger_bands_get_choices_that_are_far_apart_when_the_range_allows()
    {
        // Fidan 1-5: hedeften uzak bir sayı varsa çeldirici oradan seçiliyor.
        // Aralık dar olduğu için her zaman yetmiyor (hedef 3 iken uzak sayı
        // yok), o yüzden beklenen "elde ne varsa o kadarı".
        for (var seed = 0; seed < 40; seed++)
        {
            var round = CountMatchRound.ForBand(AgeBand.Fidan, new Random(seed));

            while (round.Current is { } question)
            {
                var target = question.Group.Count;
                var distractors = question.Choices.Where(c => c != target).ToList();

                var available = Enumerable.Range(1, 5).Count(n => Math.Abs(n - target) > 2);
                var used = distractors.Count(c => Math.Abs(c - target) > 2);

                Assert.Equal(Math.Min(distractors.Count, available), used);

                round.Drop(target);
            }
        }
    }

    [Fact]
    public void Every_object_in_a_group_looks_the_same()
    {
        // Karışık şekil "kaç tane" sorusunu sessizce "kaç tane daire"ye
        // çeviriyor; küme tek şekil ve tek renk taşıyor.
        var round = Round(AgeBand.Fidan);

        while (round.Current is { } question)
        {
            Assert.True(Enum.IsDefined(question.Group.Kind));
            Assert.True(Enum.IsDefined(question.Group.Hue));
            round.Drop(question.Group.Count);
        }
    }

    [Fact]
    public void A_wrong_digit_keeps_the_question_so_the_child_can_count_again()
    {
        var round = Round(AgeBand.Mese);
        var question = round.Current!;
        var wrong = question.Choices.First(c => c != question.Group.Count);

        Assert.Equal(CountOutcome.Wrong, round.Drop(wrong));

        Assert.Equal(question, round.Current);
        Assert.Equal(0, round.Correct);
        Assert.Equal(1, round.Mistakes);

        Assert.Equal(CountOutcome.Correct, round.Drop(question.Group.Count));
        Assert.Equal(1, round.Correct);
    }

    [Fact]
    public void Filiz_does_not_count_mistakes()
    {
        var round = Round(AgeBand.Filiz);
        var question = round.Current!;
        var wrong = question.Choices.First(c => c != question.Group.Count);

        Assert.Equal(CountOutcome.Wrong, round.Drop(wrong));
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void Dropping_on_a_digit_that_is_not_on_screen_is_ignored()
    {
        var round = Round(AgeBand.Fidan);
        var absent = Enumerable.Range(1, 5).First(n => !round.Current!.Choices.Contains(n));

        Assert.Equal(CountOutcome.Ignored, round.Drop(absent));
        Assert.Equal(0, round.Mistakes);
        Assert.Equal(0, round.Correct);
    }

    [Fact]
    public void Answering_every_question_completes_the_round()
    {
        var round = Round(AgeBand.Fidan);

        var counts = PlayThrough(round);

        Assert.True(round.IsComplete);
        Assert.Equal(round.Total, counts.Count);
        Assert.Equal(round.Total, round.Correct);
        Assert.Null(round.Current);
    }

    [Fact]
    public void A_finished_round_stops_responding()
    {
        var round = Round(AgeBand.Filiz);
        PlayThrough(round);

        Assert.Equal(CountOutcome.Ignored, round.Drop(1));
    }

    [Fact]
    public void The_same_amount_never_comes_twice_in_a_row()
    {
        // Aynı miktar tekrar ederse çocuk saymadan "yine aynısı" deyip
        // doğruyu buluyor ve oyun bir şey öğretmiyor.
        for (var seed = 0; seed < 60; seed++)
        {
            var counts = PlayThrough(CountMatchRound.ForBand(AgeBand.Filiz, new Random(seed)));

            for (var i = 1; i < counts.Count; i++)
            {
                Assert.True(
                    counts[i] != counts[i - 1],
                    $"seed {seed}: {i}. soruda miktar tekrar etti ({counts[i]})");
            }
        }
    }

    [Fact]
    public void Only_the_oldest_band_scatters_the_objects()
    {
        Assert.False(Round(AgeBand.Filiz).ScattersItems);
        Assert.False(Round(AgeBand.Fidan).ScattersItems);
        Assert.True(Round(AgeBand.Mese).ScattersItems);
    }

    [Fact]
    public void Only_the_oldest_band_races_the_clock()
    {
        Assert.Null(Round(AgeBand.Filiz).ParTime);
        Assert.Null(Round(AgeBand.Fidan).ParTime);
        Assert.NotNull(Round(AgeBand.Mese).ParTime);
    }

    [Fact]
    public void The_same_seed_produces_the_same_round()
    {
        var a = Round(AgeBand.Fidan, seed: 12).Current!;
        var b = Round(AgeBand.Fidan, seed: 12).Current!;

        Assert.Equal(a.Group, b.Group);
        Assert.Equal(a.Choices, b.Choices);
    }
}
