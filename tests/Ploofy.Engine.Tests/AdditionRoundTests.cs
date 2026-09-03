using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

/// <summary>
/// Basit Toplama: sorular banda sığıyor mu, çeldiriciler gerçekten
/// çeldiriyor mu, yanlış cevap soruyu geçiriyor mu.
/// </summary>
public class AdditionRoundTests
{
    private static AdditionRound Round(AgeBand band = AgeBand.Fidan, int seed = 3) =>
        AdditionRound.ForBand(band, new Random(seed));

    private static void AnswerAll(AdditionRound round)
    {
        while (round.Current is { } question)
        {
            round.Answer(question.Answer);
        }
    }

    [Fact]
    public void A_round_starts_with_a_question()
    {
        var round = Round();

        Assert.NotNull(round.Current);
        Assert.Equal(AdditionTuning.Questions.For(AgeBand.Fidan), round.Total);
        Assert.Equal(0, round.Answered);
        Assert.False(round.IsComplete);
    }

    [Fact]
    public void The_right_choice_moves_on()
    {
        var round = Round();
        var question = round.Current!;

        Assert.Equal(AnswerOutcome.Correct, round.Answer(question.Answer));
        Assert.Equal(1, round.Answered);
        Assert.NotSame(question, round.Current);
    }

    [Fact]
    public void A_wrong_choice_keeps_the_same_question()
    {
        // Yanlış cevap "tekrar dene" demek, "kaybettin" değil.
        var round = Round();
        var question = round.Current!;
        var wrong = question.Choices.First(c => c != question.Answer);

        Assert.Equal(AnswerOutcome.Wrong, round.Answer(wrong));
        Assert.Same(question, round.Current);
        Assert.Equal(0, round.Answered);
        Assert.Equal(1, round.WrongAnswers);
    }

    [Fact]
    public void A_number_that_is_not_on_screen_is_ignored()
    {
        var round = Round();

        Assert.Equal(AnswerOutcome.Ignored, round.Answer(999));
        Assert.Equal(0, round.WrongAnswers);
    }

    [Fact]
    public void The_round_ends_when_every_question_is_answered()
    {
        var round = Round();

        AnswerAll(round);

        Assert.True(round.IsComplete);
        Assert.Equal(round.Total, round.Answered);
        Assert.Null(round.Current);
        Assert.Equal(AnswerOutcome.Ignored, round.Answer(1));
    }

    [Theory]
    [InlineData(AgeBand.Fidan)]
    [InlineData(AgeBand.Mese)]
    public void Every_sum_stays_inside_the_band_limit(AgeBand band)
    {
        var maxSum = AdditionTuning.MaxSum.For(band);

        for (var seed = 0; seed < 40; seed++)
        {
            var round = AdditionRound.ForBand(band, new Random(seed));

            while (round.Current is { } question)
            {
                Assert.InRange(question.Answer, 2, maxSum);
                Assert.InRange(question.Left, 1, maxSum - 1);
                Assert.InRange(question.Right, 1, maxSum - 1);
                round.Answer(question.Answer);
            }
        }
    }

    [Theory]
    [InlineData(AgeBand.Fidan)]
    [InlineData(AgeBand.Mese)]
    public void Every_question_offers_the_right_number_of_distinct_choices(AgeBand band)
    {
        var expected = AdditionTuning.ChoiceCount.For(band);

        for (var seed = 0; seed < 40; seed++)
        {
            var round = AdditionRound.ForBand(band, new Random(seed));

            while (round.Current is { } question)
            {
                Assert.Equal(expected, question.Choices.Count);
                Assert.Equal(expected, question.Choices.Distinct().Count());
                Assert.Contains(question.Answer, question.Choices);
                round.Answer(question.Answer);
            }
        }
    }

    [Fact]
    public void No_choice_is_zero_or_negative()
    {
        // İki pozitif sayının toplamı oraya düşmüyor; çocuk onu bakmadan
        // eliyor ve seçenek boşa gidiyor.
        for (var seed = 0; seed < 40; seed++)
        {
            foreach (var band in new[] { AgeBand.Fidan, AgeBand.Mese })
            {
                var round = AdditionRound.ForBand(band, new Random(seed));

                while (round.Current is { } question)
                {
                    Assert.All(question.Choices, c => Assert.True(c >= 1));
                    round.Answer(question.Answer);
                }
            }
        }
    }

    [Fact]
    public void At_least_one_distractor_is_a_neighbour_of_the_answer()
    {
        // Uzak bir çeldirici toplamayı gerektirmeden eleniyor. Bir eksik ya
        // da bir fazla ise ancak gerçekten sayarak eleniyor.
        for (var seed = 0; seed < 40; seed++)
        {
            var round = AdditionRound.ForBand(AgeBand.Mese, new Random(seed));

            while (round.Current is { } question)
            {
                Assert.Contains(
                    question.Choices,
                    c => c != question.Answer && Math.Abs(c - question.Answer) == 1);

                round.Answer(question.Answer);
            }
        }
    }

    [Fact]
    public void The_same_answer_does_not_repeat_back_to_back()
    {
        // Tekrarlarsa çocuk soruya bakmadan önceki kutucuğa dokunmayı
        // öğreniyor. Dar aralıkta garanti edilemiyor, o yüzden ölçüt
        // "hiç olmasın" değil "nadir olsun".
        var repeats = 0;
        var transitions = 0;

        for (var seed = 0; seed < 60; seed++)
        {
            var round = AdditionRound.ForBand(AgeBand.Mese, new Random(seed));
            var previous = -1;

            while (round.Current is { } question)
            {
                if (previous >= 0)
                {
                    transitions++;
                    if (question.Answer == previous)
                    {
                        repeats++;
                    }
                }

                previous = question.Answer;
                round.Answer(question.Answer);
            }
        }

        Assert.True(
            repeats * 20 < transitions,
            $"{transitions} geçişin {repeats} tanesi tekrar — fazla");
    }

    [Fact]
    public void The_oldest_band_counts_on_instead_of_counting_all()
    {
        // Fidan iki kümeyi de sayıyor; Meşe birinciyi rakam olarak görüp
        // oradan devam ediyor. Saymanın bir sonraki adımı bu.
        Assert.True(Round(AgeBand.Fidan).ShowsFirstAsObjects);
        Assert.False(Round(AgeBand.Mese).ShowsFirstAsObjects);
    }

    [Fact]
    public void Every_countable_object_is_old_enough_for_the_oldest_devices()
    {
        // Uygulamanın alt sınırı Android 8.0; daha yeni emoji orada boş kutu.
        foreach (var glyph in AdditionTuning.Objects)
        {
            Assert.NotEmpty(glyph);
            foreach (var rune in glyph.EnumerateRunes())
            {
                Assert.True(rune.Value < 0x1FA00, $"{glyph}: U+{rune.Value:X} çok yeni");
            }
        }
    }
}
