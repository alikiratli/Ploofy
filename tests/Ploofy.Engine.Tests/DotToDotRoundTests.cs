using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

/// <summary>
/// Noktaları Birleştir: sıra dayatılıyor mu, yanlış nokta ne oluyor, boşluğa
/// dokunmak neden hata değil.
/// </summary>
public class DotToDotRoundTests
{
    private static DotToDotRound Round(AgeBand band = AgeBand.Fidan, int seed = 7) =>
        DotToDotRound.ForBand(band, new Random(seed));

    /// <summary>Sıradaki noktanın tam üstüne dokunur.</summary>
    private static DotTapResult TapNext(DotToDotRound round)
    {
        var dot = round.Current.Dots[round.NextDot];
        return round.Tap(dot.X, dot.Y);
    }

    private static void FinishPicture(DotToDotRound round)
    {
        var dots = round.Current.Count;
        for (var i = 0; i < dots; i++)
        {
            TapNext(round);
        }
    }

    [Fact]
    public void A_round_starts_at_the_first_dot()
    {
        var round = Round();

        Assert.Equal(0, round.NextDot);
        Assert.Equal(0, round.Completed);
        Assert.False(round.IsComplete);
        Assert.True(round.Current.Count >= 3);
    }

    [Fact]
    public void Tapping_the_next_dot_extends_the_line()
    {
        var round = Round();

        Assert.Equal(DotTapResult.Connected, TapNext(round));
        Assert.Equal(1, round.NextDot);
        Assert.Equal(1, round.Connected);
    }

    [Fact]
    public void The_order_is_enforced()
    {
        var round = Round();

        // Üçüncü noktaya birinciden atlanamıyor.
        var third = round.Current.Dots[2];

        Assert.Equal(DotTapResult.Wrong, round.Tap(third.X, third.Y));
        Assert.Equal(0, round.NextDot);
        Assert.Equal(1, round.WrongTaps);
    }

    [Fact]
    public void Tapping_empty_space_is_not_a_mistake()
    {
        // Dört yaşındaki çocuğun parmağı ekranda geziniyor. Her temasa hata
        // yazmak yıldızı beceriyle ilgisiz bir şeye bağlardı.
        var round = Round();

        // Bütün resimler 0,06-0,94 kutusunda; köşe hiçbir noktaya yakın değil.
        Assert.Equal(DotTapResult.Ignored, round.Tap(0.001f, 0.999f));
        Assert.Equal(0, round.WrongTaps);
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void The_last_dot_closes_the_picture()
    {
        var round = Round();
        var dots = round.Current.Count;

        for (var i = 0; i < dots - 1; i++)
        {
            Assert.Equal(DotTapResult.Connected, TapNext(round));
        }

        Assert.Equal(DotTapResult.PictureComplete, TapNext(round));
        Assert.True(round.PictureComplete);
        Assert.Equal(1, round.Completed);

        // Sıradaki resim baştan başlıyor.
        Assert.Equal(0, round.NextDot);
    }

    [Fact]
    public void The_round_ends_when_every_picture_is_drawn()
    {
        var round = Round();

        for (var i = 0; i < round.Total; i++)
        {
            FinishPicture(round);
        }

        Assert.True(round.IsComplete);
        Assert.Equal(round.Total, round.Completed);
        Assert.Equal(DotTapResult.Ignored, round.Tap(0.5f, 0.5f));
    }

    [Fact]
    public void The_nearest_dot_wins_not_the_first_one_in_range()
    {
        // Yengecin kıskaçlarında noktalar yan yana. İlk yeterince yakın olanı
        // seçen bir arama, parmağın gerçekten bastığı noktayı kaçırırdı.
        var round = Round(AgeBand.Mese);
        var picture = round.Current;

        var first = picture.Dots[0];
        var second = picture.Dots[1];

        // Birinci noktanın hemen yanına, ikinciye doğru bir adım.
        var x = first.X + ((second.X - first.X) * 0.05f);
        var y = first.Y + ((second.Y - first.Y) * 0.05f);

        Assert.Equal(DotTapResult.Connected, round.Tap(x, y));
    }

    [Theory]
    [InlineData(AgeBand.Fidan)]
    [InlineData(AgeBand.Mese)]
    public void Every_band_gets_pictures_inside_its_own_dot_range(AgeBand band)
    {
        var min = DotToDotTuning.MinDots.For(band);
        var max = DotToDotTuning.MaxDots.For(band);

        for (var seed = 0; seed < 30; seed++)
        {
            var round = DotToDotRound.ForBand(band, new Random(seed));

            for (var i = 0; i < round.Total; i++)
            {
                Assert.InRange(round.Current.Count, min, max);
                FinishPicture(round);
            }
        }
    }

    [Fact]
    public void Wrong_taps_only_cost_stars_in_the_oldest_band()
    {
        var young = Round();
        var third = young.Current.Dots[2];
        young.Tap(third.X, third.Y);

        Assert.Equal(1, young.WrongTaps);
        Assert.Equal(0, young.Mistakes);

        var old = Round(AgeBand.Mese);
        var otherThird = old.Current.Dots[2];
        old.Tap(otherThird.X, otherThird.Y);

        Assert.Equal(1, old.WrongTaps);
        Assert.Equal(1, old.Mistakes);
    }

    [Fact]
    public void The_youngest_band_is_shown_which_dot_comes_next()
    {
        // Fidan rakamları tanıyor ama sırayı ekranda aramak ayrı bir iş.
        // Meşe'de belirtme kapalı; orada oyun gerçekten rakam okumak.
        Assert.True(Round().HighlightsNext);
        Assert.False(Round(AgeBand.Mese).HighlightsNext);
    }

    [Fact]
    public void Every_picture_keeps_its_dots_inside_the_safe_box()
    {
        // Nokta ekranın tam kenarındaysa parmak oraya rahat basamıyor.
        foreach (var picture in DotPictures.All)
        {
            foreach (var dot in picture.Dots)
            {
                Assert.InRange(dot.X, 0.06f, 0.94f);
                Assert.InRange(dot.Y, 0.06f, 0.94f);
            }
        }
    }

    [Fact]
    public void No_two_dots_in_a_picture_sit_on_top_of_each_other()
    {
        // Üst üste iki nokta, çocuğun ayırt edemeyeceği bir seçim demek.
        // En dar bandın toleransı 0,06; noktalar arası en az o kadar açık olmalı.
        foreach (var picture in DotPictures.All)
        {
            for (var i = 0; i < picture.Count; i++)
            {
                for (var j = i + 1; j < picture.Count; j++)
                {
                    var a = picture.Dots[i];
                    var b = picture.Dots[j];
                    var distance = MathF.Sqrt(
                        ((a.X - b.X) * (a.X - b.X)) + ((a.Y - b.Y) * (a.Y - b.Y)));

                    Assert.True(
                        distance >= 0.12f,
                        $"{picture.Id}: {i + 1} ile {j + 1} çok yakın ({distance:F3})");
                }
            }
        }
    }

    [Fact]
    public void The_library_covers_both_bands()
    {
        Assert.NotEmpty(DotPictures.Between(
            DotToDotTuning.MinDots.For(AgeBand.Fidan),
            DotToDotTuning.MaxDots.For(AgeBand.Fidan)));

        Assert.NotEmpty(DotPictures.Between(
            DotToDotTuning.MinDots.For(AgeBand.Mese),
            DotToDotTuning.MaxDots.For(AgeBand.Mese)));
    }
}
