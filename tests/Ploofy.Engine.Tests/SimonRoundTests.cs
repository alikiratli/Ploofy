using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

public class SimonRoundTests
{
    private static SimonRound Round(AgeBand band, int seed = 3) =>
        SimonRound.ForBand(band, new Random(seed));

    /// <summary>Bulunulan seviyeyi baştan sona doğru tekrarlar.</summary>
    private static SimonOutcome RepeatLevel(SimonRound round)
    {
        var outcome = SimonOutcome.Ignored;

        // Dizi seviye bitince uzuyor; kopyası üzerinden gidiliyor.
        foreach (var pad in round.Sequence.ToList())
        {
            outcome = round.Tap(pad);
        }

        return outcome;
    }

    /// <summary>Turu baştan sona doğru oynar.</summary>
    private static void PlayThrough(SimonRound round)
    {
        while (!round.IsComplete)
        {
            Assert.Equal(SimonOutcome.LevelComplete, RepeatLevel(round));
        }
    }

    [Theory]
    [InlineData(AgeBand.Filiz, 3, 1, 4)]
    [InlineData(AgeBand.Fidan, 4, 2, 5)]
    [InlineData(AgeBand.Mese, 6, 3, 6)]
    public void Pads_length_and_levels_scale_with_the_band(
        AgeBand band, int pads, int startLength, int levels)
    {
        var round = Round(band);

        Assert.Equal(pads, round.Pads);
        Assert.Equal(startLength, round.Sequence.Count);
        Assert.Equal(levels, round.Total);
    }

    [Fact]
    public void The_sequence_only_ever_names_pads_that_are_on_screen()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var round = SimonRound.ForBand(AgeBand.Mese, new Random(seed));

            while (!round.IsComplete)
            {
                Assert.All(round.Sequence, pad => Assert.InRange(pad, 0, round.Pads - 1));
                RepeatLevel(round);
            }
        }
    }

    [Fact]
    public void Each_level_adds_exactly_one_step()
    {
        var round = Round(AgeBand.Mese);
        var expected = round.Sequence.Count;

        while (!round.IsComplete)
        {
            Assert.Equal(expected, round.Sequence.Count);
            RepeatLevel(round);
            expected++;
        }

        // Son seviye tamamlandığında dizi artık uzamıyor.
        Assert.Equal(expected - 1, round.Sequence.Count);
    }

    [Fact]
    public void The_old_sequence_stays_at_the_front_of_the_new_one()
    {
        // Klasik oyunun asıl kuralı: çocuk her seviyede tanıdığı bir
        // başlangıcın üstüne tek bir şey ekliyor.
        for (var seed = 0; seed < 40; seed++)
        {
            var round = SimonRound.ForBand(AgeBand.Fidan, new Random(seed));

            while (!round.IsComplete)
            {
                var before = round.Sequence.ToList();
                RepeatLevel(round);

                Assert.Equal(before, round.Sequence.Take(before.Count));
            }
        }
    }

    [Fact]
    public void Repeating_the_whole_sequence_completes_the_level()
    {
        var round = Round(AgeBand.Fidan);
        var sequence = round.Sequence.ToList();

        for (var i = 0; i < sequence.Count - 1; i++)
        {
            Assert.Equal(SimonOutcome.Correct, round.Tap(sequence[i]));
            Assert.Equal(i + 1, round.Position);
        }

        Assert.Equal(SimonOutcome.LevelComplete, round.Tap(sequence[^1]));
        Assert.Equal(1, round.Completed);
        Assert.Equal(0, round.Position);
    }

    [Fact]
    public void A_wrong_pad_rewinds_to_the_start_without_changing_the_sequence()
    {
        var round = Round(AgeBand.Mese);
        var sequence = round.Sequence.ToList();

        Assert.Equal(SimonOutcome.Correct, round.Tap(sequence[0]));

        var wrong = Enumerable.Range(0, round.Pads).First(p => p != sequence[1]);
        Assert.Equal(SimonOutcome.Wrong, round.Tap(wrong));

        Assert.Equal(0, round.Position);
        Assert.Equal(0, round.Completed);
        Assert.Equal(1, round.Mistakes);
        Assert.Equal(sequence, round.Sequence);

        // Aynı dizi bir kez daha denenebiliyor.
        Assert.Equal(SimonOutcome.LevelComplete, RepeatLevel(round));
    }

    [Fact]
    public void Filiz_does_not_count_mistakes()
    {
        var round = Round(AgeBand.Filiz);
        var wrong = Enumerable.Range(0, round.Pads).First(p => p != round.Sequence[0]);

        Assert.Equal(SimonOutcome.Wrong, round.Tap(wrong));
        Assert.Equal(0, round.Mistakes);
    }

    [Theory]
    [InlineData(-1)]
    [InlineData(99)]
    public void A_pad_that_is_not_on_screen_is_ignored(int pad)
    {
        var round = Round(AgeBand.Filiz);

        Assert.Equal(SimonOutcome.Ignored, round.Tap(pad));
        Assert.Equal(0, round.Mistakes);
        Assert.Equal(0, round.Position);
    }

    [Fact]
    public void Younger_bands_never_see_the_same_pad_twice_in_a_row()
    {
        // Aynı tuşun peş peşe yanması "iki kez mi yandı, bir kez uzun mu?"
        // sorusunu doğuruyor ve küçük çocuk bunu ayırt edemiyor.
        foreach (var band in new[] { AgeBand.Filiz, AgeBand.Fidan })
        {
            for (var seed = 0; seed < 60; seed++)
            {
                var round = SimonRound.ForBand(band, new Random(seed));
                PlayThrough(round);

                for (var i = 1; i < round.Sequence.Count; i++)
                {
                    Assert.True(
                        round.Sequence[i] != round.Sequence[i - 1],
                        $"{band} seed {seed}: {i}. adımda tuş tekrar etti");
                }
            }
        }
    }

    [Fact]
    public void The_oldest_band_may_repeat_a_pad()
    {
        // Meşe'de ayırt edilebiliyor ve dizinin gerçek zorluğunu artırıyor.
        var sawRepeat = false;

        for (var seed = 0; seed < 200 && !sawRepeat; seed++)
        {
            var round = SimonRound.ForBand(AgeBand.Mese, new Random(seed));
            PlayThrough(round);

            for (var i = 1; i < round.Sequence.Count; i++)
            {
                if (round.Sequence[i] == round.Sequence[i - 1])
                {
                    sawRepeat = true;
                    break;
                }
            }
        }

        Assert.True(sawRepeat);
    }

    [Fact]
    public void Playing_every_level_completes_the_round()
    {
        var round = Round(AgeBand.Fidan);

        PlayThrough(round);

        Assert.True(round.IsComplete);
        Assert.Equal(round.Total, round.Completed);
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void A_finished_round_stops_responding()
    {
        var round = Round(AgeBand.Filiz);
        PlayThrough(round);

        Assert.Equal(SimonOutcome.Ignored, round.Tap(round.Sequence[0]));
    }

    [Fact]
    public void The_screen_plays_faster_for_older_children()
    {
        Assert.True(Round(AgeBand.Filiz).StepDuration > Round(AgeBand.Fidan).StepDuration);
        Assert.True(Round(AgeBand.Fidan).StepDuration > Round(AgeBand.Mese).StepDuration);
    }

    [Fact]
    public void The_same_seed_produces_the_same_sequence()
    {
        var a = Round(AgeBand.Mese, seed: 21);
        var b = Round(AgeBand.Mese, seed: 21);

        Assert.Equal(a.Sequence, b.Sequence);
    }
}
