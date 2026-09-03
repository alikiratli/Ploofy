using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

/// <summary>
/// Kategori Ayırma: kutular dengeli mi, yanlış kutu parçayı kaybettiriyor mu,
/// Meşe gerçekten daha zor mu.
/// </summary>
public class CategorySortRoundTests
{
    private static CategorySortRound Round(AgeBand band = AgeBand.Fidan, int seed = 5) =>
        CategorySortRound.ForBand(band, new Random(seed));

    private static void SortAll(CategorySortRound round)
    {
        while (round.Current is { } item)
        {
            round.Drop(item.CategoryId);
        }
    }

    [Fact]
    public void A_round_starts_with_an_item_and_a_set_of_bins()
    {
        var round = Round();

        Assert.NotNull(round.Current);
        Assert.Equal(CategorySortTuning.BinCount.For(AgeBand.Fidan), round.Bins.Count);
        Assert.Equal(round.Bins.Count, round.Bins.Distinct().Count());
        Assert.False(round.IsComplete);
    }

    [Fact]
    public void Every_item_belongs_to_one_of_the_bins()
    {
        // Ekranda kutusu olmayan bir parça çıkarsa çocuk çözümsüz kalır.
        for (var seed = 0; seed < 40; seed++)
        {
            foreach (var band in Enum.GetValues<AgeBand>())
            {
                var round = CategorySortRound.ForBand(band, new Random(seed));

                while (round.Current is { } item)
                {
                    Assert.Contains(item.CategoryId, round.Bins);
                    round.Drop(item.CategoryId);
                }
            }
        }
    }

    [Fact]
    public void The_right_bin_takes_the_item()
    {
        var round = Round();
        var item = round.Current!;

        Assert.Equal(DropOutcome.Sorted, round.Drop(item.CategoryId));
        Assert.Equal(1, round.Sorted);
        Assert.NotEqual(item.Id, round.Current?.Id);
    }

    [Fact]
    public void The_wrong_bin_keeps_the_item_in_place()
    {
        // Parça kaybolmuyor: çocuk aynı parçayı doğru kutuya koyana kadar
        // deneyebiliyor.
        var round = Round();
        var item = round.Current!;
        var wrong = round.Bins.First(b => b != item.CategoryId);

        Assert.Equal(DropOutcome.WrongBin, round.Drop(wrong));
        Assert.Equal(item.Id, round.Current!.Id);
        Assert.Equal(0, round.Sorted);
        Assert.Equal(1, round.WrongDrops);
    }

    [Fact]
    public void A_bin_that_is_not_on_screen_is_ignored()
    {
        var round = Round();

        Assert.Equal(DropOutcome.Ignored, round.Drop("bir_yerde_olmayan"));
        Assert.Equal(0, round.WrongDrops);
    }

    [Fact]
    public void Wrong_bins_only_cost_stars_from_the_middle_band_up()
    {
        // Filiz'de yanlış kutu denemek öğrenmenin kendisi.
        var young = Round(AgeBand.Filiz);
        var youngItem = young.Current!;
        young.Drop(young.Bins.First(b => b != youngItem.CategoryId));

        Assert.Equal(1, young.WrongDrops);
        Assert.Equal(0, young.Mistakes);

        var older = Round(AgeBand.Fidan);
        var olderItem = older.Current!;
        older.Drop(older.Bins.First(b => b != olderItem.CategoryId));

        Assert.Equal(1, older.WrongDrops);
        Assert.Equal(1, older.Mistakes);
    }

    [Fact]
    public void The_round_ends_when_every_item_is_sorted()
    {
        var round = Round();
        var total = round.Total;

        SortAll(round);

        Assert.True(round.IsComplete);
        Assert.Equal(total, round.Sorted);
        Assert.Null(round.Current);
        Assert.Equal(DropOutcome.Ignored, round.Drop(round.Bins[0]));
    }

    [Fact]
    public void Every_bin_gets_the_same_number_of_items()
    {
        // Bir kategoriden tek parça gelirse o kutu turun sonuna kadar boş
        // duruyor ve çocuk kutunun bozuk olduğunu sanıyor.
        for (var seed = 0; seed < 20; seed++)
        {
            var round = CategorySortRound.ForBand(AgeBand.Mese, new Random(seed));
            var counts = new Dictionary<string, int>();

            while (round.Current is { } item)
            {
                counts[item.CategoryId] = counts.GetValueOrDefault(item.CategoryId) + 1;
                round.Drop(item.CategoryId);
            }

            Assert.Equal(round.Bins.Count, counts.Count);
            Assert.Single(counts.Values.Distinct());
        }
    }

    [Fact]
    public void The_oldest_band_always_gets_a_fine_grained_category()
    {
        // Rastgele seçim bazen üç kaba kategori getiriyordu ve o tur
        // Fidan'dan farksız oluyordu.
        var fine = ItemCategories.All
            .Where(c => c.MinBand == AgeBand.Mese)
            .Select(c => c.Id)
            .ToHashSet();

        for (var seed = 0; seed < 30; seed++)
        {
            var round = CategorySortRound.ForBand(AgeBand.Mese, new Random(seed));
            Assert.Contains(round.Bins, fine.Contains);
        }
    }

    [Fact]
    public void The_younger_bands_never_see_the_fine_grained_categories()
    {
        for (var seed = 0; seed < 30; seed++)
        {
            foreach (var band in new[] { AgeBand.Filiz, AgeBand.Fidan })
            {
                var round = CategorySortRound.ForBand(band, new Random(seed));
                Assert.All(
                    round.Bins,
                    id => Assert.NotEqual(AgeBand.Mese, ItemCategories.Find(id)!.MinBand));
            }
        }
    }

    [Fact]
    public void No_item_appears_in_two_categories()
    {
        // Aynı emoji iki kümede olsaydı doğru cevap iki tane olurdu.
        var all = ItemCategories.All.SelectMany(c => c.Items).ToList();

        Assert.Equal(all.Count, all.Distinct().Count());
    }

    [Fact]
    public void Every_category_has_enough_members_for_the_biggest_round()
    {
        // Meşe turu kutu başına dört parça istiyor (12 / 3).
        var perBin = CategorySortTuning.ItemCount.For(AgeBand.Mese)
            / CategorySortTuning.BinCount.For(AgeBand.Mese);

        Assert.All(ItemCategories.All, c => Assert.True(
            c.Items.Count >= perBin,
            $"{c.Id}: {c.Items.Count} üye, en az {perBin} gerekiyor"));
    }

    [Fact]
    public void No_glyph_comes_from_a_code_block_that_is_too_new()
    {
        // U+1FA00 ve üstü tümüyle Unicode 12+; uygulamanın alt sınırı Android
        // 8.0 (Unicode 10) ve orada boş kutu çıkar. Bu ağ Unicode 11
        // eklemelerini yakalamıyor — onlar eski blokların arasına serpilmiş —
        // ama en sık yapılan hatayı, yeni bloktan emoji seçmeyi durduruyor.
        foreach (var category in ItemCategories.All)
        {
            foreach (var glyph in category.Items)
            {
                foreach (var rune in glyph.EnumerateRunes())
                {
                    Assert.True(
                        rune.Value < 0x1FA00,
                        $"{category.Id}: {glyph} içinde U+{rune.Value:X} çok yeni");
                }
            }
        }
    }
}
