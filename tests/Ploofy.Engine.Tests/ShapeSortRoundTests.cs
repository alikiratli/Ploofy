using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

public class ShapeSortRoundTests
{
    private static ShapeSortRound Round(AgeBand band, int seed = 5) =>
        ShapeSortRound.ForBand(band, new Random(seed));

    /// <summary>Turu baştan sona doğru oynar.</summary>
    private static void PlayThrough(ShapeSortRound round)
    {
        while (round.Current is { } piece)
        {
            Assert.Equal(DropOutcome.Sorted, round.Drop(piece.Kind));
        }
    }

    [Theory]
    [InlineData(AgeBand.Filiz, 2, 6)]
    [InlineData(AgeBand.Fidan, 3, 9)]
    [InlineData(AgeBand.Mese, 4, 12)]
    public void Bins_and_pieces_scale_with_the_band(AgeBand band, int bins, int pieces)
    {
        var round = Round(band);

        Assert.Equal(bins, round.Bins.Count);
        Assert.Equal(pieces, round.Total);
    }

    [Fact]
    public void Every_bin_gets_the_same_number_of_pieces()
    {
        // Bir şekilden tek parça gelirse o kutu tur boyunca boş duruyor ve
        // çocuk kutunun bozuk olduğunu sanıyor.
        var round = Round(AgeBand.Mese);

        var counts = new Dictionary<ShapeKind, int>();
        while (round.Current is { } piece)
        {
            counts[piece.Kind] = counts.GetValueOrDefault(piece.Kind) + 1;
            round.Drop(piece.Kind);
        }

        Assert.Equal(round.Bins.Count, counts.Count);
        Assert.All(counts.Values, count => Assert.Equal(3, count));
    }

    [Fact]
    public void Pieces_only_ever_belong_to_a_bin_on_screen()
    {
        var round = Round(AgeBand.Fidan);

        while (round.Current is { } piece)
        {
            Assert.Contains(piece.Kind, round.Bins);
            round.Drop(piece.Kind);
        }
    }

    [Fact]
    public void Filiz_gets_a_matching_colour_for_every_shape()
    {
        // Renk ve şekil aynı şeyi söylüyor: çocuk hangisine bakarsa baksın
        // doğru kutuyu buluyor.
        var round = Round(AgeBand.Filiz);
        Assert.True(round.ColorMatchesShape);

        var hueByKind = new Dictionary<ShapeKind, BubbleHue>();
        while (round.Current is { } piece)
        {
            if (hueByKind.TryGetValue(piece.Kind, out var hue))
            {
                Assert.Equal(hue, piece.Hue);
            }
            else
            {
                hueByKind[piece.Kind] = piece.Hue;
            }

            round.Drop(piece.Kind);
        }

        // İki şeklin rengi de birbirinden farklı olmalı, yoksa ipucu işe yaramaz.
        Assert.Equal(hueByKind.Count, hueByKind.Values.Distinct().Count());
    }

    [Fact]
    public void Older_bands_have_to_look_at_the_shape()
    {
        Assert.False(Round(AgeBand.Fidan).ColorMatchesShape);
        Assert.False(Round(AgeBand.Mese).ColorMatchesShape);
    }

    [Fact]
    public void A_wrong_bin_keeps_the_piece_so_the_child_can_try_again()
    {
        var round = Round(AgeBand.Mese);
        var piece = round.Current!;
        var wrongBin = round.Bins.First(b => b != piece.Kind);

        Assert.Equal(DropOutcome.WrongBin, round.Drop(wrongBin));

        Assert.Equal(piece, round.Current);
        Assert.Equal(0, round.Sorted);
        Assert.Equal(1, round.Mistakes);

        Assert.Equal(DropOutcome.Sorted, round.Drop(piece.Kind));
        Assert.Equal(1, round.Sorted);
    }

    [Fact]
    public void Filiz_does_not_count_wrong_bins()
    {
        // Bu bantta yanlış kutu denemek öğrenmenin kendisi.
        var round = Round(AgeBand.Filiz);
        var piece = round.Current!;
        var wrongBin = round.Bins.First(b => b != piece.Kind);

        Assert.Equal(DropOutcome.WrongBin, round.Drop(wrongBin));
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void Dropping_into_a_bin_that_is_not_on_screen_is_ignored()
    {
        var round = Round(AgeBand.Filiz);
        var absent = Enum.GetValues<ShapeKind>().First(k => !round.Bins.Contains(k));

        Assert.Equal(DropOutcome.Ignored, round.Drop(absent));
        Assert.Equal(0, round.Mistakes);
        Assert.Equal(0, round.Sorted);
    }

    [Fact]
    public void Sorting_every_piece_completes_the_round()
    {
        var round = Round(AgeBand.Fidan);

        PlayThrough(round);

        Assert.True(round.IsComplete);
        Assert.Equal(round.Total, round.Sorted);
        Assert.Equal(0, round.Remaining);
        Assert.Null(round.Current);
        Assert.Null(round.Next);
    }

    [Fact]
    public void A_finished_round_stops_responding()
    {
        var round = Round(AgeBand.Filiz);
        PlayThrough(round);

        Assert.Equal(DropOutcome.Ignored, round.Drop(round.Bins[0]));
    }

    [Fact]
    public void The_next_piece_is_visible_before_it_arrives()
    {
        // Arayüz sıradakini arkada soluk gösteriyor; oyun akışı böyle
        // duraksamıyor.
        var round = Round(AgeBand.Fidan);

        var next = round.Next;
        Assert.NotNull(next);

        round.Drop(round.Current!.Kind);
        Assert.Equal(next, round.Current);
    }

    [Fact]
    public void The_same_shape_never_comes_three_times_in_a_row()
    {
        // Sıra rastgele ama "hep aynı kutu" hissi vermemeli.
        for (var seed = 0; seed < 60; seed++)
        {
            var round = ShapeSortRound.ForBand(AgeBand.Mese, new Random(seed));

            var kinds = new List<ShapeKind>();
            while (round.Current is { } piece)
            {
                kinds.Add(piece.Kind);
                round.Drop(piece.Kind);
            }

            for (var i = 2; i < kinds.Count; i++)
            {
                var isRun = kinds[i] == kinds[i - 1] && kinds[i] == kinds[i - 2];
                Assert.False(isRun, $"seed {seed}: {i}. sırada üçlü dizi var");
            }
        }
    }

    [Fact]
    public void The_same_seed_produces_the_same_round()
    {
        var a = Round(AgeBand.Fidan, seed: 9);
        var b = Round(AgeBand.Fidan, seed: 9);

        Assert.Equal(a.Bins, b.Bins);
        Assert.Equal(a.Current, b.Current);
    }

    [Fact]
    public void Only_the_oldest_band_races_the_clock()
    {
        Assert.Null(Round(AgeBand.Filiz).ParTime);
        Assert.Null(Round(AgeBand.Fidan).ParTime);
        Assert.NotNull(Round(AgeBand.Mese).ParTime);
    }
}
