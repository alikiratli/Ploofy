using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

public class JigsawRoundTests
{
    private static JigsawRound Round(AgeBand band, int seed = 8) =>
        JigsawRound.ForBand(band, new Random(seed));

    /// <summary>Turu baştan sona doğru oynar ve yerleştirme sırasını döner.</summary>
    private static List<JigsawPiece> PlayThrough(JigsawRound round)
    {
        var order = new List<JigsawPiece>();

        while (round.Current is { } piece)
        {
            order.Add(piece);
            Assert.Equal(PlaceOutcome.Fitted, round.Place(piece.Row, piece.Column));
        }

        return order;
    }

    [Theory]
    [InlineData(AgeBand.Filiz, 2, 4)]
    [InlineData(AgeBand.Fidan, 3, 9)]
    [InlineData(AgeBand.Mese, 4, 16)]
    public void The_grid_scales_with_the_band(AgeBand band, int grid, int total)
    {
        var round = Round(band);

        Assert.Equal(grid, round.Grid);
        Assert.Equal(total, round.Total);
        Assert.Equal(total, round.Pieces.Count);
        Assert.Equal(total, round.Tray.Count);
    }

    [Fact]
    public void Every_slot_has_exactly_one_piece()
    {
        var round = Round(AgeBand.Mese);

        for (var row = 0; row < round.Grid; row++)
        {
            for (var column = 0; column < round.Grid; column++)
            {
                var piece = round.PieceAt(row, column);
                Assert.Equal(row, piece.Row);
                Assert.Equal(column, piece.Column);
            }
        }

        Assert.Equal(round.Total, round.Pieces.Select(p => p.Id).Distinct().Count());
    }

    [Fact]
    public void The_outer_frame_is_cut_straight()
    {
        // Dış çerçevede tırnak olmaz; köşe parçası düz iki kenarıyla
        // tahtanın neresine gittiğini kendi söylüyor.
        var round = Round(AgeBand.Mese);
        var last = round.Grid - 1;

        foreach (var piece in round.Pieces)
        {
            if (piece.Row == 0)
            {
                Assert.Equal(0, piece.Top);
            }

            if (piece.Row == last)
            {
                Assert.Equal(0, piece.Bottom);
            }

            if (piece.Column == 0)
            {
                Assert.Equal(0, piece.Left);
            }

            if (piece.Column == last)
            {
                Assert.Equal(0, piece.Right);
            }
        }
    }

    [Fact]
    public void Neighbouring_edges_always_fit_into_each_other()
    {
        // Parçanın çizimi de yuvasının çizimi de aynı sayılardan türüyor;
        // ters olmazlarsa ekranda üst üste biniyorlar.
        for (var seed = 0; seed < 40; seed++)
        {
            var round = JigsawRound.ForBand(AgeBand.Mese, new Random(seed));

            for (var row = 0; row < round.Grid; row++)
            {
                for (var column = 0; column < round.Grid; column++)
                {
                    var piece = round.PieceAt(row, column);

                    if (column < round.Grid - 1)
                    {
                        var right = round.PieceAt(row, column + 1);
                        Assert.Equal(piece.Right, -right.Left);
                        Assert.NotEqual(0, piece.Right);
                    }

                    if (row < round.Grid - 1)
                    {
                        var below = round.PieceAt(row + 1, column);
                        Assert.Equal(piece.Bottom, -below.Top);
                        Assert.NotEqual(0, piece.Bottom);
                    }
                }
            }
        }
    }

    [Fact]
    public void Every_piece_comes_to_the_tray_exactly_once()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            foreach (var band in Enum.GetValues<AgeBand>())
            {
                var round = JigsawRound.ForBand(band, new Random(seed));
                var order = PlayThrough(round);

                Assert.Equal(round.Total, order.Count);
                Assert.Equal(round.Total, order.Select(p => p.Id).Distinct().Count());
            }
        }
    }

    [Fact]
    public void Without_a_ghost_the_puzzle_grows_out_from_a_corner()
    {
        // Ortadan gelen yalnız bir parçanın nereye gideceğini çıkarmanın
        // yolu yok: etrafında bakılacak hiçbir şey olmuyor.
        for (var seed = 0; seed < 40; seed++)
        {
            var round = JigsawRound.ForBand(AgeBand.Mese, new Random(seed));
            Assert.False(round.ShowsGhost);

            var order = round.Tray.ToList();
            var last = round.Grid - 1;

            var first = order[0];
            Assert.True(
                (first.Row == 0 || first.Row == last) &&
                (first.Column == 0 || first.Column == last),
                $"seed {seed}: ilk parça köşe değil ({first.Row},{first.Column})");

            for (var i = 1; i < order.Count; i++)
            {
                var piece = order[i];
                var hasNeighbour = order
                    .Take(i)
                    .Any(placed =>
                        Math.Abs(placed.Row - piece.Row) +
                        Math.Abs(placed.Column - piece.Column) == 1);

                Assert.True(
                    hasNeighbour,
                    $"seed {seed}: {i}. parçanın yerleşmiş komşusu yok");
            }
        }
    }

    [Fact]
    public void With_a_ghost_the_order_is_free()
    {
        // Hayalet varken oyun "resmi eşleştir"; komşuya ihtiyaç yok, o yüzden
        // sıra da kısıtlanmıyor. Rastgeleliğin gerçekten kullanıldığını
        // görmek için birden fazla tohuma bakılıyor.
        var starts = new HashSet<int>();

        for (var seed = 0; seed < 40; seed++)
        {
            var round = JigsawRound.ForBand(AgeBand.Fidan, new Random(seed));
            Assert.True(round.ShowsGhost);
            starts.Add(round.Tray[0].Id);
        }

        Assert.True(starts.Count > 4, "sıra rastgele görünmüyor");
    }

    [Fact]
    public void A_wrong_slot_keeps_the_piece_so_the_child_can_try_again()
    {
        var round = Round(AgeBand.Mese);
        var piece = round.Current!;

        var wrongRow = piece.Row == 0 ? 1 : 0;
        Assert.Equal(PlaceOutcome.WrongSlot, round.Place(wrongRow, piece.Column));

        Assert.Same(piece, round.Current);
        Assert.False(piece.IsPlaced);
        Assert.Equal(0, round.Placed);
        Assert.Equal(1, round.Mistakes);

        Assert.Equal(PlaceOutcome.Fitted, round.Place(piece.Row, piece.Column));
        Assert.Equal(1, round.Placed);
        Assert.True(piece.IsPlaced);
    }

    [Fact]
    public void Filiz_does_not_count_wrong_slots()
    {
        var round = Round(AgeBand.Filiz);
        var piece = round.Current!;

        var wrongRow = piece.Row == 0 ? 1 : 0;
        Assert.Equal(PlaceOutcome.WrongSlot, round.Place(wrongRow, piece.Column));
        Assert.Equal(0, round.Mistakes);
    }

    [Theory]
    [InlineData(-1, 0)]
    [InlineData(0, -1)]
    [InlineData(99, 0)]
    [InlineData(0, 99)]
    public void A_slot_outside_the_board_is_ignored(int row, int column)
    {
        var round = Round(AgeBand.Fidan);

        Assert.Equal(PlaceOutcome.Ignored, round.Place(row, column));
        Assert.Equal(0, round.Mistakes);
        Assert.Equal(0, round.Placed);
    }

    [Fact]
    public void The_next_piece_is_visible_before_it_arrives()
    {
        // Arayüz sıradakini arkada soluk gösteriyor; oyun akışı duraksamıyor.
        var round = Round(AgeBand.Fidan);

        var next = round.Next;
        Assert.NotNull(next);

        round.Place(round.Current!.Row, round.Current.Column);
        Assert.Same(next, round.Current);
    }

    [Fact]
    public void Placing_every_piece_completes_the_round()
    {
        var round = Round(AgeBand.Fidan);

        PlayThrough(round);

        Assert.True(round.IsComplete);
        Assert.Equal(round.Total, round.Placed);
        Assert.Empty(round.Tray);
        Assert.Null(round.Current);
        Assert.Null(round.Next);
        Assert.All(round.Pieces, piece => Assert.True(piece.IsPlaced));
    }

    [Fact]
    public void A_finished_round_stops_responding()
    {
        var round = Round(AgeBand.Filiz);
        PlayThrough(round);

        Assert.Equal(PlaceOutcome.Ignored, round.Place(0, 0));
    }

    [Fact]
    public void The_snap_area_gets_tighter_with_age()
    {
        // Parçayı yuvanın tam ortasına bırakmak bu yaşta beklenemez.
        Assert.True(Round(AgeBand.Filiz).SnapReach > Round(AgeBand.Fidan).SnapReach);
        Assert.True(Round(AgeBand.Fidan).SnapReach > Round(AgeBand.Mese).SnapReach);
    }

    [Fact]
    public void Only_the_oldest_band_races_the_clock()
    {
        Assert.Null(Round(AgeBand.Filiz).ParTime);
        Assert.Null(Round(AgeBand.Fidan).ParTime);
        Assert.NotNull(Round(AgeBand.Mese).ParTime);
    }

    [Fact]
    public void The_same_seed_produces_the_same_puzzle()
    {
        var a = Round(AgeBand.Fidan, seed: 44);
        var b = Round(AgeBand.Fidan, seed: 44);

        Assert.Equal(a.PictureSeed, b.PictureSeed);
        Assert.Equal(a.Tray.Select(p => p.Id), b.Tray.Select(p => p.Id));
        Assert.Equal(
            a.Pieces.Select(p => (p.Top, p.Right, p.Bottom, p.Left)),
            b.Pieces.Select(p => (p.Top, p.Right, p.Bottom, p.Left)));
    }
}
