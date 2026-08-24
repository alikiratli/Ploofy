using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

public class HuntRoundTests
{
    private static readonly string[] Letters =
        ["A", "B", "C", "D", "E", "F", "G", "M", "N", "O", "P", "R", "S", "W", "Z"];

    private static readonly string[] Numbers =
        ["0", "1", "2", "3", "4", "5", "6", "7", "8", "9"];

    private static HuntRound Round(
        AgeBand band,
        HuntKind kind = HuntKind.Letter,
        int seed = 4) =>
        HuntRound.ForBand(
            band,
            kind,
            kind == HuntKind.Letter ? Letters : Numbers,
            new Random(seed));

    /// <summary>Turu baştan sona doğru oynar.</summary>
    private static void PlayThrough(HuntRound round)
    {
        while (round.Current is { } question)
        {
            var target = question.Choices.Single(c => c.IsTarget);
            Assert.Equal(HuntOutcome.Correct, round.Tap(target.Id));
        }
    }

    [Theory]
    [InlineData(AgeBand.Filiz, 4, 2)]
    [InlineData(AgeBand.Fidan, 6, 4)]
    [InlineData(AgeBand.Mese, 10, 6)]
    public void Questions_and_choices_scale_with_the_band(AgeBand band, int questions, int choices)
    {
        var round = Round(band);

        Assert.Equal(questions, round.Total);
        Assert.Equal(choices, round.Current!.Choices.Count);
    }

    [Fact]
    public void Every_question_has_exactly_one_right_answer()
    {
        var round = Round(AgeBand.Mese);

        while (round.Current is { } question)
        {
            Assert.Single(question.Choices, c => c.IsTarget);
            Assert.Equal(question.Target, question.Choices.Single(c => c.IsTarget).Glyph);

            round.Tap(question.Choices.Single(c => c.IsTarget).Id);
        }
    }

    [Fact]
    public void Choices_never_repeat_a_glyph()
    {
        // Aynı harf iki kez görünürse "hangisi doğru?" sorusunun cevabı yok.
        var round = Round(AgeBand.Mese);

        while (round.Current is { } question)
        {
            var glyphs = question.Choices.Select(c => c.Glyph).ToList();
            Assert.Equal(glyphs.Count, glyphs.Distinct(StringComparer.Ordinal).Count());

            round.Tap(question.Choices.Single(c => c.IsTarget).Id);
        }
    }

    [Fact]
    public void Choices_only_come_from_the_pool()
    {
        // Havuz dile göre değişiyor; Türkçe turda Almanca bir harf çıkmamalı.
        var round = Round(AgeBand.Mese);

        while (round.Current is { } question)
        {
            Assert.All(question.Choices, c => Assert.Contains(c.Glyph, Letters));
            round.Tap(question.Choices.Single(c => c.IsTarget).Id);
        }
    }

    [Fact]
    public void Oak_pulls_distractors_from_look_alikes()
    {
        // Rastgele çeldirici arasından bulmak tanıma; benzeyeni ayırmak
        // okumaya geçişin gerçek eşiği.
        var round = HuntRound.ForBand(AgeBand.Mese, HuntKind.Number, Numbers, new Random(1));

        var lookAlikePairs = new Dictionary<string, string[]>(StringComparer.Ordinal)
        {
            ["6"] = ["9", "8", "5"],
            ["9"] = ["6", "8", "0"],
            ["2"] = ["5", "7", "3"],
            ["5"] = ["2", "6"],
            ["3"] = ["8", "2"],
            ["8"] = ["3", "6", "9"],
            ["1"] = ["7", "4"],
            ["7"] = ["1", "2"],
            ["0"] = ["8", "9", "6"],
            ["4"] = ["1", "9"],
        };

        var sawLookAlike = false;

        while (round.Current is { } question)
        {
            if (lookAlikePairs.TryGetValue(question.Target, out var similar))
            {
                var others = question.Choices.Where(c => !c.IsTarget).Select(c => c.Glyph);
                if (others.Any(g => similar.Contains(g, StringComparer.Ordinal)))
                {
                    sawLookAlike = true;
                }
            }

            round.Tap(question.Choices.Single(c => c.IsTarget).Id);
        }

        Assert.True(sawLookAlike, "Meşe turunda hiç benzer çeldirici çıkmadı");
    }

    [Fact]
    public void Younger_bands_do_not_get_look_alike_distractors_on_purpose()
    {
        // Fidan'da amaç tanıma; ayırt etme henüz erken.
        Assert.False(HuntTuning.UseConfusables.For(AgeBand.Fidan));
        Assert.False(HuntTuning.UseConfusables.For(AgeBand.Filiz));
        Assert.True(HuntTuning.UseConfusables.For(AgeBand.Mese));
    }

    [Fact]
    public void A_wrong_tap_keeps_the_question_so_the_child_finds_the_answer()
    {
        var round = Round(AgeBand.Mese);
        var question = round.Current!;
        var wrong = question.Choices.First(c => !c.IsTarget);

        Assert.Equal(HuntOutcome.Wrong, round.Tap(wrong.Id));

        Assert.Same(question, round.Current);
        Assert.Equal(0, round.Correct);
        Assert.Equal(1, round.Mistakes);

        Assert.Equal(HuntOutcome.Correct, round.Tap(question.Choices.Single(c => c.IsTarget).Id));
        Assert.Equal(1, round.Correct);
    }

    [Fact]
    public void Filiz_does_not_count_wrong_taps()
    {
        var round = Round(AgeBand.Filiz);
        var wrong = round.Current!.Choices.First(c => !c.IsTarget);

        Assert.Equal(HuntOutcome.Wrong, round.Tap(wrong.Id));
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void Tapping_something_that_is_not_on_screen_is_ignored()
    {
        var round = Round(AgeBand.Fidan);

        Assert.Equal(HuntOutcome.Ignored, round.Tap(999));
        Assert.Equal(0, round.Mistakes);
        Assert.Equal(0, round.Correct);
    }

    [Fact]
    public void Answering_every_question_completes_the_round()
    {
        var round = Round(AgeBand.Fidan);

        PlayThrough(round);

        Assert.True(round.IsComplete);
        Assert.Equal(round.Total, round.Correct);
        Assert.Null(round.Current);
    }

    [Fact]
    public void A_finished_round_stops_responding()
    {
        var round = Round(AgeBand.Filiz);
        PlayThrough(round);

        Assert.Equal(HuntOutcome.Ignored, round.Tap(0));
    }

    [Fact]
    public void A_pool_smaller_than_the_band_wants_still_works()
    {
        // Küçük bir alfabede (ör. yalnızca sesli harfler) oyun kırılmamalı,
        // sadece daha az seçenek göstermeli.
        var round = HuntRound.ForBand(AgeBand.Mese, HuntKind.Letter, ["A", "E", "I"], new Random(2));

        Assert.Equal(3, round.Current!.Choices.Count);
        Assert.Single(round.Current.Choices, c => c.IsTarget);
    }

    [Fact]
    public void A_pool_of_one_is_rejected()
    {
        var ex = Assert.Throws<ArgumentException>(
            () => HuntRound.ForBand(AgeBand.Fidan, HuntKind.Letter, ["A"]));

        Assert.Equal("pool", ex.ParamName);
    }

    [Fact]
    public void The_same_seed_produces_the_same_round()
    {
        var a = Round(AgeBand.Mese, seed: 8);
        var b = Round(AgeBand.Mese, seed: 8);

        Assert.Equal(a.Current!.Target, b.Current!.Target);
        Assert.Equal(
            a.Current.Choices.Select(c => c.Glyph),
            b.Current.Choices.Select(c => c.Glyph));
    }

    [Fact]
    public void Only_the_oldest_band_races_the_clock()
    {
        Assert.Null(Round(AgeBand.Filiz).ParTime);
        Assert.Null(Round(AgeBand.Fidan).ParTime);
        Assert.NotNull(Round(AgeBand.Mese).ParTime);
    }
}
