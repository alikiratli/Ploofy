using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

public class MemoryMatchRoundTests
{
    private static readonly string[] Symbols =
        ["kedi", "kopek", "kus", "balik", "tavsan", "ari", "at", "inek", "koyun", "kaplumbaga", "fil", "aslan"];

    private static MemoryMatchRound Round(AgeBand band, int seed = 42) =>
        MemoryMatchRound.ForBand(band, Symbols, new Random(seed));

    /// <summary>Verilen sembolün iki kartından ikincisinin konumu.</summary>
    private static (int First, int Second) PairPositions(MemoryMatchRound round, string symbol)
    {
        var positions = round.Cards
            .Where(c => c.SymbolId == symbol)
            .Select(c => c.Index)
            .ToArray();
        return (positions[0], positions[1]);
    }

    [Theory]
    [InlineData(AgeBand.Filiz, 3)]
    [InlineData(AgeBand.Fidan, 6)]
    [InlineData(AgeBand.Mese, 10)]
    public void Deck_size_scales_with_the_band(AgeBand band, int expectedPairs)
    {
        var round = Round(band);

        Assert.Equal(expectedPairs, round.TotalPairs);
        Assert.Equal(expectedPairs * 2, round.Cards.Count);
    }

    [Fact]
    public void Every_symbol_appears_exactly_twice()
    {
        var round = Round(AgeBand.Mese);

        var counts = round.Cards.GroupBy(c => c.SymbolId).Select(g => g.Count());
        Assert.All(counts, count => Assert.Equal(2, count));
    }

    [Fact]
    public void A_pool_smaller_than_the_band_needs_is_rejected()
    {
        // Sessizce daha az çiftle oynamak yerine hata: eksik içerik erken görülsün.
        var ex = Assert.Throws<ArgumentException>(
            () => MemoryMatchRound.ForBand(AgeBand.Mese, ["kedi", "kopek"]));

        Assert.Equal("symbolPool", ex.ParamName);
    }

    [Fact]
    public void Matching_two_cards_keeps_them_face_up_and_clears_the_pending_pair()
    {
        var round = Round(AgeBand.Fidan);
        var symbol = round.Cards[0].SymbolId;
        var (first, second) = PairPositions(round, symbol);

        Assert.Equal(FlipResult.AwaitingSecond, round.Flip(first));
        Assert.Equal(FlipResult.Matched, round.Flip(second));

        Assert.Contains(first, round.MatchedIndices);
        Assert.Contains(second, round.MatchedIndices);
        Assert.Empty(round.FaceUpIndices);
        Assert.Equal(1, round.MatchedPairs);
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void A_mismatch_waits_for_the_ui_to_close_it()
    {
        var round = Round(AgeBand.Fidan);
        var (first, _) = PairPositions(round, round.Cards[0].SymbolId);
        var other = round.Cards.First(c => c.SymbolId != round.Cards[first].SymbolId).Index;

        Assert.Equal(FlipResult.AwaitingSecond, round.Flip(first));
        Assert.Equal(FlipResult.Mismatched, round.Flip(other));

        // İki kart açık dururken üçüncü çevirme kabul edilmez.
        var third = round.Cards.First(c => c.Index != first && c.Index != other).Index;
        Assert.Equal(FlipResult.Ignored, round.Flip(third));

        round.CloseMismatch();
        Assert.Empty(round.FaceUpIndices);
        Assert.Equal(FlipResult.AwaitingSecond, round.Flip(third));
    }

    [Fact]
    public void Filiz_does_not_count_mistakes()
    {
        // Bu yaşta kartı yanlış açmak oyunun kendisi, hata değil.
        var round = Round(AgeBand.Filiz);
        var (first, _) = PairPositions(round, round.Cards[0].SymbolId);
        var other = round.Cards.First(c => c.SymbolId != round.Cards[first].SymbolId).Index;

        round.Flip(first);
        Assert.Equal(FlipResult.Mismatched, round.Flip(other));

        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void Older_bands_count_mistakes()
    {
        var round = Round(AgeBand.Mese);
        var (first, _) = PairPositions(round, round.Cards[0].SymbolId);
        var other = round.Cards.First(c => c.SymbolId != round.Cards[first].SymbolId).Index;

        round.Flip(first);
        round.Flip(other);

        Assert.Equal(1, round.Mistakes);
    }

    [Fact]
    public void Flipping_an_already_revealed_or_out_of_range_card_is_ignored()
    {
        var round = Round(AgeBand.Fidan);

        Assert.Equal(FlipResult.AwaitingSecond, round.Flip(0));
        Assert.Equal(FlipResult.Ignored, round.Flip(0));
        Assert.Equal(FlipResult.Ignored, round.Flip(-1));
        Assert.Equal(FlipResult.Ignored, round.Flip(round.Cards.Count));
    }

    [Fact]
    public void Matching_every_pair_completes_the_round()
    {
        var round = Round(AgeBand.Filiz);

        foreach (var symbol in round.Cards.Select(c => c.SymbolId).Distinct())
        {
            var (first, second) = PairPositions(round, symbol);
            round.Flip(first);
            round.Flip(second);
        }

        Assert.True(round.IsComplete);
        Assert.Equal(round.TotalPairs, round.MatchedPairs);
    }

    [Fact]
    public void The_same_seed_produces_the_same_board()
    {
        // Testler ve "aynı tahtayı tekrar oyna" özelliği buna dayanıyor.
        var a = Round(AgeBand.Fidan, seed: 7);
        var b = Round(AgeBand.Fidan, seed: 7);

        Assert.Equal(
            a.Cards.Select(c => c.SymbolId),
            b.Cards.Select(c => c.SymbolId));
    }
}
