using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

public class PatternRoundTests
{
    private static PatternRound Round(AgeBand band, int seed = 7) =>
        PatternRound.ForBand(band, new Random(seed));

    private static PatternChoice Answer(PatternQuestion question) =>
        question.Choices.Single(c => c.IsCorrect);

    /// <summary>Boşluk doğru parçayla dolmuş hâli.</summary>
    private static List<PatternTile> Filled(PatternQuestion question)
    {
        var filled = new List<PatternTile>();
        for (var i = 0; i < question.Sequence.Count; i++)
        {
            filled.Add(i == question.BlankIndex
                ? Answer(question).Tile
                : question.Sequence[i]!.Value);
        }

        return filled;
    }

    /// <summary>Dizinin en küçük tekrar periyodu.</summary>
    private static int Period(IReadOnlyList<PatternTile> tiles)
    {
        for (var p = 1; p <= tiles.Count; p++)
        {
            var repeats = true;
            for (var i = 0; i + p < tiles.Count; i++)
            {
                if (tiles[i] != tiles[i + p])
                {
                    repeats = false;
                    break;
                }
            }

            if (repeats)
            {
                return p;
            }
        }

        return tiles.Count;
    }

    private static void PlayThrough(PatternRound round)
    {
        while (round.Current is { } question)
        {
            Assert.Equal(PatternOutcome.Correct, round.Tap(Answer(question).Id));
        }
    }

    [Theory]
    [InlineData(AgeBand.Filiz, 4)]
    [InlineData(AgeBand.Fidan, 6)]
    [InlineData(AgeBand.Mese, 8)]
    public void The_number_of_questions_scales_with_the_band(AgeBand band, int questions) =>
        Assert.Equal(questions, Round(band).Total);

    [Theory]
    [InlineData(AgeBand.Filiz, 2)]
    [InlineData(AgeBand.Fidan, 3)]
    [InlineData(AgeBand.Mese, 4)]
    public void The_number_of_choices_scales_with_the_band(AgeBand band, int choices) =>
        Assert.Equal(choices, Round(band).Current!.Choices.Count);

    [Theory]
    [InlineData(AgeBand.Filiz, 6)]
    [InlineData(AgeBand.Fidan, 8)]
    [InlineData(AgeBand.Mese, 9)]
    public void The_sequence_length_scales_with_the_band(AgeBand band, int length) =>
        Assert.Equal(length, Round(band).Current!.Sequence.Count);

    [Fact]
    public void There_is_exactly_one_blank_and_the_index_points_at_it()
    {
        foreach (var band in Enum.GetValues<AgeBand>())
        {
            for (var seed = 0; seed < 40; seed++)
            {
                var round = PatternRound.ForBand(band, new Random(seed));

                while (round.Current is { } question)
                {
                    Assert.Single(question.Sequence, t => t is null);
                    Assert.Null(question.Sequence[question.BlankIndex]);

                    round.Tap(Answer(question).Id);
                }
            }
        }
    }

    [Fact]
    public void The_answer_is_the_tile_that_continues_the_pattern()
    {
        // Oyunun tek iddiası bu: boşluğa konan parça diziyi tekrar eden bir
        // dizi yapıyor. Bozulursa oyun kura çekmeye dönüşür ve bunu ekranda
        // fark etmek zor.
        foreach (var band in Enum.GetValues<AgeBand>())
        {
            for (var seed = 0; seed < 40; seed++)
            {
                var round = PatternRound.ForBand(band, new Random(seed));

                while (round.Current is { } question)
                {
                    var period = Period(Filled(question));
                    Assert.InRange(period, 2, 4);

                    round.Tap(Answer(question).Id);
                }
            }
        }
    }

    [Fact]
    public void At_least_one_whole_unit_is_visible_before_the_blank()
    {
        // Aksi hâlde örüntü diziden okunamıyor ve soru bilmeceye değil kura
        // çekmeye dönüyor.
        for (var seed = 0; seed < 60; seed++)
        {
            var round = PatternRound.ForBand(AgeBand.Mese, new Random(seed));

            while (round.Current is { } question)
            {
                var period = Period(Filled(question));
                Assert.True(
                    question.BlankIndex >= period,
                    $"boşluk {question.BlankIndex}, periyot {period}");

                round.Tap(Answer(question).Id);
            }
        }
    }

    [Fact]
    public void Only_the_oldest_band_hides_the_blank_inside_the_sequence()
    {
        // Sondaki boşluk "sırada ne var" sorusu; ortadaki "burada ne eksik".
        // İkincisi belirgin biçimde zor: sağdaki parçaları da hesaba katmak
        // gerekiyor.
        foreach (var band in new[] { AgeBand.Filiz, AgeBand.Fidan })
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var round = PatternRound.ForBand(band, new Random(seed));

                while (round.Current is { } question)
                {
                    Assert.Equal(question.Sequence.Count - 1, question.BlankIndex);
                    round.Tap(Answer(question).Id);
                }
            }
        }

        var inside = 0;
        for (var seed = 0; seed < 60; seed++)
        {
            var round = PatternRound.ForBand(AgeBand.Mese, new Random(seed));

            while (round.Current is { } question)
            {
                if (question.BlankIndex < question.Sequence.Count - 1)
                {
                    inside++;
                }

                round.Tap(Answer(question).Id);
            }
        }

        Assert.True(inside > 0);
    }

    [Fact]
    public void The_youngest_band_only_changes_colour_never_shape()
    {
        // İki boyutta birden değişen bir dizi, örüntüyü henüz kavramamış bir
        // çocuk için iki ayrı bilmece.
        for (var seed = 0; seed < 30; seed++)
        {
            var round = PatternRound.ForBand(AgeBand.Filiz, new Random(seed));

            while (round.Current is { } question)
            {
                var kinds = Filled(question)
                    .Concat(question.Choices.Select(c => c.Tile))
                    .Select(t => t.Kind)
                    .Distinct();

                Assert.Single(kinds);
                round.Tap(Answer(question).Id);
            }
        }
    }

    [Fact]
    public void The_older_bands_do_change_shape()
    {
        var varied = false;

        for (var seed = 0; seed < 30 && !varied; seed++)
        {
            var round = PatternRound.ForBand(AgeBand.Mese, new Random(seed));

            while (round.Current is { } question)
            {
                if (Filled(question).Select(t => t.Kind).Distinct().Count() > 1)
                {
                    varied = true;
                }

                round.Tap(Answer(question).Id);
            }
        }

        Assert.True(varied);
    }

    [Fact]
    public void Exactly_one_choice_is_right_and_no_two_choices_are_alike()
    {
        // Aynı parçadan iki tane olsaydı biri "yanlış" sayılırdı ve çocuk
        // doğruya basıp yanlış cevap almış olurdu.
        foreach (var band in Enum.GetValues<AgeBand>())
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var round = PatternRound.ForBand(band, new Random(seed));

                while (round.Current is { } question)
                {
                    Assert.Single(question.Choices, c => c.IsCorrect);

                    var tiles = question.Choices.Select(c => c.Tile).ToList();
                    Assert.Equal(tiles.Count, tiles.Distinct().Count());

                    var ids = question.Choices.Select(c => c.Id).ToList();
                    Assert.Equal(ids.Count, ids.Distinct().Count());

                    round.Tap(Answer(question).Id);
                }
            }
        }
    }

    [Fact]
    public void A_wrong_choice_leaves_the_question_on_the_screen()
    {
        // Doğruyu görmeden geçmek, oyunun öğretici olma iddiasını boşa
        // çıkarırdı.
        var round = Round(AgeBand.Mese);
        var question = round.Current!;
        var wrong = question.Choices.First(c => !c.IsCorrect);

        Assert.Equal(PatternOutcome.Wrong, round.Tap(wrong.Id));

        Assert.Same(question, round.Current);
        Assert.Equal(0, round.Correct);
        Assert.Equal(1, round.Mistakes);
    }

    [Fact]
    public void The_youngest_band_pays_nothing_for_a_wrong_choice()
    {
        var round = Round(AgeBand.Filiz);
        var wrong = round.Current!.Choices.First(c => !c.IsCorrect);

        Assert.Equal(PatternOutcome.Wrong, round.Tap(wrong.Id));
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void A_choice_that_is_not_on_the_screen_does_nothing()
    {
        var round = Round(AgeBand.Fidan);

        Assert.Equal(PatternOutcome.Ignored, round.Tap(-1));
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void Answering_every_question_completes_the_round()
    {
        var round = Round(AgeBand.Fidan);

        PlayThrough(round);

        Assert.True(round.IsComplete);
        Assert.Equal(round.Total, round.Correct);
        Assert.Equal(0, round.Mistakes);
        Assert.Null(round.Current);
    }

    [Fact]
    public void A_finished_round_stops_responding()
    {
        var round = Round(AgeBand.Filiz);
        PlayThrough(round);

        Assert.Equal(PatternOutcome.Ignored, round.Tap(0));
    }

    [Fact]
    public void Only_the_oldest_band_races_the_clock()
    {
        Assert.Null(Round(AgeBand.Filiz).ParTime);
        Assert.Null(Round(AgeBand.Fidan).ParTime);
        Assert.NotNull(Round(AgeBand.Mese).ParTime);
    }

    [Fact]
    public void The_same_seed_produces_the_same_question()
    {
        var a = Round(AgeBand.Mese, seed: 19).Current!;
        var b = Round(AgeBand.Mese, seed: 19).Current!;

        Assert.Equal(a.Sequence, b.Sequence);
        Assert.Equal(a.BlankIndex, b.BlankIndex);
        Assert.Equal(
            a.Choices.Select(c => c.Tile),
            b.Choices.Select(c => c.Tile));
    }

    [Fact]
    public void A_round_is_not_the_same_pattern_eight_times()
    {
        // Motor aynı birimi arka arkaya vermiyor; dışarıdan görünen sonucu
        // bu. (Birimin kendisi dışarı açılmıyor: periyot birimi ayırt
        // etmiyor — AAB ile ABB'nin ikisi de üç.)
        for (var seed = 0; seed < 40; seed++)
        {
            var round = PatternRound.ForBand(AgeBand.Mese, new Random(seed));
            var periods = new List<int>();

            while (round.Current is { } question)
            {
                periods.Add(Period(Filled(question)));
                round.Tap(Answer(question).Id);
            }

            Assert.True(periods.Distinct().Count() > 1, $"tohum {seed}: tek örüntü");
        }
    }

    [Fact]
    public void The_youngest_band_gets_the_same_unit_every_time_on_purpose()
    {
        // Filiz yalnızca AB görüyor: bu bantta amaç örüntü kurmak değil,
        // "bir şey tekrar ediyor" fikrini yakalamak. Parçalar yine de her
        // soruda değişiyor.
        for (var seed = 0; seed < 20; seed++)
        {
            var round = PatternRound.ForBand(AgeBand.Filiz, new Random(seed));

            while (round.Current is { } question)
            {
                Assert.Equal(2, Period(Filled(question)));
                round.Tap(Answer(question).Id);
            }
        }
    }
}
