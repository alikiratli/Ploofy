using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

public class LineUpRoundTests
{
    private static LineUpRound Round(AgeBand band, int seed = 5) =>
        LineUpRound.ForBand(band, new Random(seed));

    /// <summary>Bulmacayı doğru sırayla çözer.</summary>
    private static void SolvePuzzle(LineUpRound round)
    {
        while (!round.PuzzleSolved)
        {
            var piece = round.Tray[0];
            Assert.Equal(PlaceOutcome.Fitted, round.Place(piece.Id, piece.Rank));
        }
    }

    private static void PlayThrough(LineUpRound round)
    {
        while (!round.IsComplete)
        {
            SolvePuzzle(round);
            round.NextPuzzle();
        }
    }

    /// <summary>Sıralanan özelliğin değeri: boyut ya da miktar.</summary>
    private static float SortedValue(LineUpPiece piece, SortAttribute attribute) =>
        attribute == SortAttribute.Size ? piece.Size : piece.Count;

    [Theory]
    [InlineData(AgeBand.Filiz, 3)]
    [InlineData(AgeBand.Fidan, 4)]
    [InlineData(AgeBand.Mese, 5)]
    public void The_number_of_puzzles_scales_with_the_band(AgeBand band, int puzzles) =>
        Assert.Equal(puzzles, Round(band).Total);

    [Theory]
    [InlineData(AgeBand.Filiz, 3)]
    [InlineData(AgeBand.Fidan, 4)]
    [InlineData(AgeBand.Mese, 5)]
    public void The_number_of_pieces_scales_with_the_band(AgeBand band, int pieces)
    {
        var round = Round(band);

        Assert.Equal(pieces, round.Tray.Count);
        Assert.Equal(pieces, round.Slots.Count);
    }

    [Fact]
    public void The_youngest_band_sorts_by_size_and_never_has_to_count()
    {
        // İki yaşındaki çocuk saymıyor ama büyüğü küçükten ayırıyor.
        var round = Round(AgeBand.Filiz);

        Assert.Equal(SortAttribute.Size, round.Attribute);
        Assert.All(round.Tray, p => Assert.Equal(1, p.Count));
    }

    [Fact]
    public void The_older_bands_sort_by_quantity()
    {
        // Say ve Eşleştir'in öğrettiği saymanın bir sonraki adımı: sayıları
        // birbiriyle kıyaslamak.
        foreach (var band in new[] { AgeBand.Fidan, AgeBand.Mese })
        {
            var round = Round(band);

            Assert.Equal(SortAttribute.Quantity, round.Attribute);
            Assert.All(round.Tray, p => Assert.Equal(1f, p.Size));
            Assert.True(round.Tray.Select(p => p.Count).Distinct().Count() == round.Tray.Count);
        }
    }

    [Fact]
    public void Only_the_sorted_property_ever_changes()
    {
        // İki boyutta birden değişen bir dizi, iki ayrı bilmece demek.
        foreach (var band in Enum.GetValues<AgeBand>())
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var round = LineUpRound.ForBand(band, new Random(seed));

                while (!round.IsComplete)
                {
                    Assert.Single(round.Tray.Select(p => p.Kind).Distinct());
                    Assert.Single(round.Tray.Select(p => p.Hue).Distinct());

                    if (round.Attribute == SortAttribute.Quantity)
                    {
                        Assert.Single(round.Tray.Select(p => p.Size).Distinct());
                    }
                    else
                    {
                        Assert.Single(round.Tray.Select(p => p.Count).Distinct());
                    }

                    SolvePuzzle(round);
                    round.NextPuzzle();
                }
            }
        }
    }

    [Fact]
    public void The_ranks_really_do_put_the_pieces_in_order()
    {
        // Oyunun tek iddiası bu. Bozulursa çocuk doğru sıralar ama oyun
        // "yanlış" der — ve bunu ekranda anlamak imkânsız.
        foreach (var band in Enum.GetValues<AgeBand>())
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var round = LineUpRound.ForBand(band, new Random(seed));

                while (!round.IsComplete)
                {
                    var ordered = round.Tray
                        .OrderBy(p => p.Rank)
                        .Select(p => SortedValue(p, round.Attribute))
                        .ToList();

                    var expected = round.Direction == SortDirection.Ascending
                        ? ordered.OrderBy(v => v).ToList()
                        : ordered.OrderByDescending(v => v).ToList();

                    Assert.Equal(expected, ordered);

                    SolvePuzzle(round);
                    round.NextPuzzle();
                }
            }
        }
    }

    [Fact]
    public void Every_slot_has_exactly_one_piece_that_fits_it()
    {
        for (var seed = 0; seed < 25; seed++)
        {
            var round = LineUpRound.ForBand(AgeBand.Mese, new Random(seed));
            var ranks = round.Tray.Select(p => p.Rank).OrderBy(r => r);

            Assert.Equal(Enumerable.Range(0, round.Slots.Count), ranks);
        }
    }

    [Fact]
    public void Only_the_oldest_band_ever_sorts_the_other_way_round()
    {
        // Değişen yön küçük bantta öğretmiyor, şaşırtıyor.
        foreach (var band in new[] { AgeBand.Filiz, AgeBand.Fidan })
        {
            for (var seed = 0; seed < 25; seed++)
            {
                var round = LineUpRound.ForBand(band, new Random(seed));

                while (!round.IsComplete)
                {
                    Assert.Equal(SortDirection.Ascending, round.Direction);
                    SolvePuzzle(round);
                    round.NextPuzzle();
                }
            }
        }

        var descending = 0;
        for (var seed = 0; seed < 40; seed++)
        {
            var round = LineUpRound.ForBand(AgeBand.Mese, new Random(seed));

            while (!round.IsComplete)
            {
                if (round.Direction == SortDirection.Descending)
                {
                    descending++;
                }

                SolvePuzzle(round);
                round.NextPuzzle();
            }
        }

        Assert.True(descending > 0);
    }

    [Fact]
    public void The_oldest_band_has_to_count_because_the_amounts_are_consecutive()
    {
        // Fidan'da fark bakışla görülüyor; Meşe'de gerçekten saymak gerekiyor
        // ve oyunun öğretici tarafı da o.
        for (var seed = 0; seed < 20; seed++)
        {
            var mese = LineUpRound.ForBand(AgeBand.Mese, new Random(seed));
            var counts = mese.Tray.Select(p => p.Count).OrderBy(c => c).ToList();

            for (var i = 1; i < counts.Count; i++)
            {
                Assert.Equal(1, counts[i] - counts[i - 1]);
            }

            var fidan = LineUpRound.ForBand(AgeBand.Fidan, new Random(seed));
            var spread = fidan.Tray.Select(p => p.Count).OrderBy(c => c).ToList();

            for (var i = 1; i < spread.Count; i++)
            {
                Assert.Equal(2, spread[i] - spread[i - 1]);
            }
        }
    }

    [Fact]
    public void The_amounts_never_run_past_the_bands_ceiling()
    {
        foreach (var (band, ceiling) in new[]
                 {
                     (AgeBand.Fidan, 8),
                     (AgeBand.Mese, 12),
                 })
        {
            for (var seed = 0; seed < 30; seed++)
            {
                var round = LineUpRound.ForBand(band, new Random(seed));

                while (!round.IsComplete)
                {
                    Assert.All(round.Tray, p => Assert.InRange(p.Count, 1, ceiling));
                    SolvePuzzle(round);
                    round.NextPuzzle();
                }
            }
        }
    }

    [Fact]
    public void A_piece_can_go_into_any_slot_not_just_the_leftmost()
    {
        // Sıralamanın tek doğru yolu yok: en büyüğü ilk gören çocuğa "önce en
        // küçüğü bul" demek gereksiz.
        var round = Round(AgeBand.Fidan);
        var last = round.Tray.Single(p => p.Rank == round.Slots.Count - 1);

        Assert.Equal(PlaceOutcome.Fitted, round.Place(last.Id, last.Rank));
        Assert.Equal(last, round.Slots[^1]);
        Assert.Null(round.Slots[0]);
    }

    [Fact]
    public void A_wrong_slot_sends_the_piece_back_to_the_tray()
    {
        var round = Round(AgeBand.Mese);
        var piece = round.Tray.Single(p => p.Rank == 0);
        var trayBefore = round.Tray.Count;

        Assert.Equal(PlaceOutcome.WrongSlot, round.Place(piece.Id, 1));

        Assert.Equal(trayBefore, round.Tray.Count);
        Assert.All(round.Slots, slot => Assert.Null(slot));
        Assert.Equal(1, round.Mistakes);
    }

    [Fact]
    public void The_youngest_band_pays_nothing_for_a_wrong_slot()
    {
        var round = Round(AgeBand.Filiz);
        var piece = round.Tray.Single(p => p.Rank == 0);

        Assert.Equal(PlaceOutcome.WrongSlot, round.Place(piece.Id, 2));
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void A_filled_slot_does_not_take_a_second_piece()
    {
        var round = Round(AgeBand.Fidan);
        var first = round.Tray.Single(p => p.Rank == 0);
        round.Place(first.Id, 0);

        var second = round.Tray.Single(p => p.Rank == 1);

        Assert.Equal(PlaceOutcome.Ignored, round.Place(second.Id, 0));
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void A_piece_that_is_not_in_the_tray_does_nothing()
    {
        var round = Round(AgeBand.Fidan);

        Assert.Equal(PlaceOutcome.Ignored, round.Place(-1, 0));
        Assert.Equal(PlaceOutcome.Ignored, round.Place(round.Tray[0].Id, 99));
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void A_solved_puzzle_stays_on_the_screen_until_the_page_asks_for_the_next()
    {
        // Motor kendiliğinden geçseydi çocuk tamamladığı diziyi hiç
        // görmezdi — ve bitirmenin ödülü tam olarak o.
        var round = Round(AgeBand.Fidan);

        SolvePuzzle(round);

        Assert.True(round.PuzzleSolved);
        Assert.Equal(1, round.Completed);
        Assert.Empty(round.Tray);
        Assert.All(round.Slots, slot => Assert.NotNull(slot));

        round.NextPuzzle();

        Assert.False(round.PuzzleSolved);
        Assert.Equal(round.Slots.Count, round.Tray.Count);
        Assert.All(round.Slots, slot => Assert.Null(slot));
    }

    [Fact]
    public void Asking_for_the_next_puzzle_too_early_does_nothing()
    {
        // Yarım bırakılmış bir diziyi kazayla silmek, çocuğun yaptığı işi
        // yok etmek olurdu.
        var round = Round(AgeBand.Fidan);
        var first = round.Tray.Single(p => p.Rank == 0);
        round.Place(first.Id, 0);

        round.NextPuzzle();

        Assert.Equal(first, round.Slots[0]);
        Assert.Equal(0, round.Completed);
    }

    [Fact]
    public void Solving_every_puzzle_completes_the_round()
    {
        var round = Round(AgeBand.Filiz);

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

        Assert.Equal(PlaceOutcome.Ignored, round.Place(0, 0));
    }

    [Fact]
    public void Only_the_oldest_band_races_the_clock()
    {
        Assert.Null(Round(AgeBand.Filiz).ParTime);
        Assert.Null(Round(AgeBand.Fidan).ParTime);
        Assert.NotNull(Round(AgeBand.Mese).ParTime);
    }

    [Fact]
    public void Piece_ids_stay_unique_across_puzzles()
    {
        // Arayüz sürüklenen parçayı kimliğinden tanıyor; bir sonraki
        // bulmacada aynı kimlik dönerse eski parça yeniden canlanır.
        var round = Round(AgeBand.Mese);
        var seen = new List<int>();

        while (!round.IsComplete)
        {
            seen.AddRange(round.Tray.Select(p => p.Id));
            SolvePuzzle(round);
            round.NextPuzzle();
        }

        Assert.Equal(seen.Count, seen.Distinct().Count());
    }

    [Fact]
    public void The_same_seed_produces_the_same_puzzle()
    {
        var a = Round(AgeBand.Mese, seed: 21);
        var b = Round(AgeBand.Mese, seed: 21);

        Assert.Equal(a.Direction, b.Direction);
        Assert.Equal(
            a.Tray.Select(p => (p.Kind, p.Hue, p.Count, p.Size, p.Rank)),
            b.Tray.Select(p => (p.Kind, p.Hue, p.Count, p.Size, p.Rank)));
    }
}
