using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

/// <summary>
/// Boyama: dokunulan yerin altındaki alan doğru bulunuyor mu, üstteki alan
/// alttakini gölgeliyor mu, resim ne zaman bitiyor.
/// </summary>
public class ColoringRoundTests
{
    private static ColoringRound Round(AgeBand band = AgeBand.Filiz, int seed = 4) =>
        ColoringRound.ForBand(band, new Random(seed));

    /// <summary>Ekranı tararken kullanılan ızgara adımı.</summary>
    /// <remarks>
    /// İki yüz kırk adım, kütüphanedeki en küçük alanı (balığın gözü,
    /// yarıçapı 0,045) rahatça yakalıyor.
    /// </remarks>
    private const int Grid = 240;

    /// <summary>Alanın içinde kalan bir nokta — köşelerin ortalaması.</summary>
    /// <remarks>
    /// Alanın <b>içinde</b> olduğunu söylüyor ama <b>üstünde</b> olduğunu
    /// söylemiyor: evin duvarının ortalama noktası kapının içine düşüyor.
    /// Dokunmayı taklit etmek için <see cref="TouchThatReaches"/> gerekiyor.
    /// </remarks>
    private static (float X, float Y) Inside(ColoringRegion region)
    {
        var x = region.Outline.Average(p => p.X);
        var y = region.Outline.Average(p => p.Y);
        return (x, y);
    }

    /// <summary>
    /// Bu alana gerçekten ulaşan bir dokunuş noktası; yoksa null.
    /// </summary>
    /// <remarks>
    /// Üstteki alanlar altındakini gölgeliyor, yani bir alana "ait" olmak
    /// yetmiyor — dokunulduğunda seçilen alan olması gerekiyor. Izgara
    /// taraması, çocuğun parmağının yapabileceğini taklit ediyor.
    /// </remarks>
    private static (float X, float Y)? TouchThatReaches(
        ColoringPicture picture, ColoringRegion region)
    {
        for (var i = 1; i < Grid; i++)
        {
            for (var j = 1; j < Grid; j++)
            {
                var x = i / (float)Grid;
                var y = j / (float)Grid;

                if (picture.HitTest(x, y)?.Id == region.Id)
                {
                    return (x, y);
                }
            }
        }

        return null;
    }

    private static void PaintWholePicture(ColoringRound round)
    {
        var picture = round.Current;

        foreach (var region in picture.Regions.ToList())
        {
            var touch = TouchThatReaches(picture, region);
            Assert.NotNull(touch);
            round.Paint(touch!.Value.X, touch.Value.Y);
        }
    }

    [Fact]
    public void A_round_starts_with_a_picture_and_nothing_painted()
    {
        var round = Round();

        Assert.NotEmpty(round.Current.Regions);
        Assert.Equal(0, round.PaintedRegions);
        Assert.Equal(0, round.Completed);
        Assert.False(round.IsComplete);
    }

    [Fact]
    public void Touching_a_region_paints_it_in_the_selected_colour()
    {
        var round = Round();
        round.SelectColor(3);

        var region = round.Current.Regions[0];
        var (x, y) = TouchThatReaches(round.Current, region)!.Value;

        Assert.Equal(PaintOutcome.Painted, round.Paint(x, y));
        Assert.Equal(3, round.ColorOf(region.Id));
        Assert.Equal(1, round.PaintedRegions);
    }

    [Fact]
    public void Touching_empty_space_does_nothing()
    {
        var round = Round();

        // Bütün sayfalar 0-1 kutusunun içinde; köşe hiçbir alana ait değil.
        Assert.Equal(PaintOutcome.Missed, round.Paint(0.001f, 0.001f));
        Assert.Equal(0, round.PaintedRegions);
    }

    [Fact]
    public void Painting_again_changes_the_colour_and_is_not_a_mistake()
    {
        // Fikir değiştirmek serbest oyunun kendisi.
        var round = Round();
        var region = round.Current.Regions[0];
        var (x, y) = TouchThatReaches(round.Current, region)!.Value;

        round.SelectColor(1);
        round.Paint(x, y);
        round.SelectColor(4);
        round.Paint(x, y);

        Assert.Equal(4, round.ColorOf(region.Id));
        Assert.Equal(1, round.PaintedRegions);
    }

    [Fact]
    public void A_colour_outside_the_palette_is_ignored()
    {
        var round = Round();
        round.SelectColor(2);

        round.SelectColor(-1);
        Assert.Equal(2, round.SelectedColor);

        round.SelectColor(ColoringTuning.PaletteSize);
        Assert.Equal(2, round.SelectedColor);
    }

    [Fact]
    public void The_picture_is_done_when_every_region_has_been_painted()
    {
        var round = Round();
        var picture = round.Current;
        var regions = picture.Regions.ToList();

        for (var i = 0; i < regions.Count - 1; i++)
        {
            var touch = TouchThatReaches(picture, regions[i])!.Value;
            Assert.Equal(PaintOutcome.Painted, round.Paint(touch.X, touch.Y));
        }

        var last = TouchThatReaches(picture, regions[^1])!.Value;
        Assert.Equal(PaintOutcome.PictureComplete, round.Paint(last.X, last.Y));

        Assert.True(round.PictureComplete);
        Assert.Equal(1, round.Completed);

        // Sıradaki resim boş başlıyor.
        Assert.Equal(0, round.PaintedRegions);
    }

    [Fact]
    public void The_round_ends_when_every_picture_is_done()
    {
        var round = Round();

        for (var i = 0; i < round.Total; i++)
        {
            PaintWholePicture(round);
        }

        Assert.True(round.IsComplete);
        Assert.Equal(round.Total, round.Completed);
        Assert.Equal(PaintOutcome.Missed, round.Paint(0.5f, 0.5f));
    }

    [Fact]
    public void The_region_on_top_wins()
    {
        // Evin kapısı duvarın üstünde: duvara ait bir noktaya dokunmak
        // kapının içindeyse kapıyı boyamalı, duvarı değil.
        var house = ColoringPictures.Find("house")!;
        var door = house.Regions.First(r => r.Id == "door");
        var (x, y) = Inside(door);

        Assert.True(house.Regions.First(r => r.Id == "wall").Contains(x, y));
        Assert.Equal("door", house.HitTest(x, y)!.Id);
    }

    [Fact]
    public void Every_region_can_be_reached_by_a_touch()
    {
        // Tamamen gölgede kalan bir alan resmi bitirilemez yapardı: çocuk ona
        // hiç dokunamaz, son alan hiç boyanmaz ve tur asla bitmezdi. Bu,
        // sayfaların uyması gereken tek gerçek kısıt.
        foreach (var picture in ColoringPictures.All)
        {
            foreach (var region in picture.Regions)
            {
                Assert.True(
                    TouchThatReaches(picture, region) is not null,
                    $"{picture.Id}/{region.Id}: üstündeki alanlar tamamen örtüyor");
            }
        }
    }

    [Fact]
    public void A_region_can_be_hidden_under_another_and_still_be_paintable()
    {
        // Evin duvarının ortalama noktası kapının içine düşüyor. Duvar yine
        // de boyanabilir olmalı — sayfayı bitirilemez yapan şey, alanın
        // TAMAMEN örtülmesi.
        var house = ColoringPictures.Find("house")!;
        var wall = house.Regions.First(r => r.Id == "wall");
        var (avgX, avgY) = Inside(wall);

        Assert.True(wall.Contains(avgX, avgY));
        Assert.Equal("door", house.HitTest(avgX, avgY)!.Id);
        Assert.NotNull(TouchThatReaches(house, wall));
    }

    [Fact]
    public void Every_region_contains_its_own_average_point()
    {
        // Testlerin dokunduğu nokta bu; alanın dışına düşerse testler
        // yanlış şeyi ölçüyor demektir.
        foreach (var picture in ColoringPictures.All)
        {
            foreach (var region in picture.Regions)
            {
                var (x, y) = Inside(region);
                Assert.True(region.Contains(x, y), $"{picture.Id}/{region.Id}");
            }
        }
    }

    [Fact]
    public void Region_keys_are_unique_inside_a_picture()
    {
        // Dolgu kaydı anahtara bağlı; iki alan aynı anahtarı taşırsa biri
        // ötekinin rengini alır ve resim hiç bitmez.
        foreach (var picture in ColoringPictures.All)
        {
            var ids = picture.Regions.Select(r => r.Id).ToList();
            Assert.Equal(ids.Count, ids.Distinct().Count());
        }
    }

    [Fact]
    public void Every_outline_stays_inside_the_canvas()
    {
        foreach (var picture in ColoringPictures.All)
        {
            foreach (var point in picture.Regions.SelectMany(r => r.Outline))
            {
                Assert.InRange(point.X, 0f, 1f);
                Assert.InRange(point.Y, 0f, 1f);
            }
        }
    }

    [Fact]
    public void The_youngest_band_only_gets_the_simple_pictures()
    {
        // On alanlı bir çiçek iki yaşındaki çocuğun bitiremeyeceği kadar uzun.
        var limit = ColoringTuning.MaxRegions.For(AgeBand.Filiz);

        for (var seed = 0; seed < 20; seed++)
        {
            var round = ColoringRound.ForBand(AgeBand.Filiz, new Random(seed));

            for (var i = 0; i < round.Total; i++)
            {
                Assert.True(round.Current.RegionCount <= limit);
                PaintWholePicture(round);
            }
        }
    }

    [Fact]
    public void There_are_pictures_for_every_band()
    {
        Assert.All(
            Enum.GetValues<AgeBand>(),
            band => Assert.NotEmpty(
                ColoringPictures.UpTo(ColoringTuning.MaxRegions.For(band))));
    }
}
