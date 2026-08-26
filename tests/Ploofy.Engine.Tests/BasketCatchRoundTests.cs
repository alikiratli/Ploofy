using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

public class BasketCatchRoundTests
{
    private static readonly TimeSpan Frame = TimeSpan.FromMilliseconds(16);

    private static BasketCatchRound Round(AgeBand band, int seed = 4) =>
        BasketCatchRound.ForBand(band, new Random(seed));

    /// <summary>Sepeti hep en alttaki nesnenin altında tutarak oynar.</summary>
    private static void PlayPerfectly(BasketCatchRound round, int maxFrames = 4000)
    {
        for (var i = 0; i < maxFrames && !round.IsComplete; i++)
        {
            var next = round.Items
                .OrderByDescending(item => item.Y)
                .FirstOrDefault();

            if (next is not null)
            {
                round.MoveBasketTo(next.X);
            }

            round.Advance(Frame);
        }
    }

    /// <summary>Sepete hiç dokunmadan, kaçmalarını bekleyerek ilerletir.</summary>
    private static void RunFrames(BasketCatchRound round, int frames)
    {
        for (var i = 0; i < frames; i++)
        {
            round.Advance(Frame);
        }
    }

    [Theory]
    [InlineData(AgeBand.Filiz, 8, 3)]
    [InlineData(AgeBand.Fidan, 10, 4)]
    [InlineData(AgeBand.Mese, 14, 6)]
    public void Goal_and_traffic_scale_with_the_band(AgeBand band, int goal, int maxItems)
    {
        var round = Round(band);

        Assert.Equal(goal, round.Goal);
        Assert.Equal(maxItems, round.MaxItems);
    }

    [Fact]
    public void The_basket_gets_narrower_and_the_fall_gets_faster_with_age()
    {
        // Zorluğun yarısı sepetin darlığında: dar sepet nesnenin nereye
        // düşeceğini önceden kestirmeyi gerektiriyor.
        Assert.True(Round(AgeBand.Filiz).BasketWidth > Round(AgeBand.Fidan).BasketWidth);
        Assert.True(Round(AgeBand.Fidan).BasketWidth > Round(AgeBand.Mese).BasketWidth);

        Assert.True(Round(AgeBand.Filiz).FallSpeed < Round(AgeBand.Fidan).FallSpeed);
        Assert.True(Round(AgeBand.Fidan).FallSpeed < Round(AgeBand.Mese).FallSpeed);
    }

    [Fact]
    public void There_is_something_falling_from_the_first_frame()
    {
        // Boş bir ekrana bakarak beklemek oyunun başlamadığı hissini veriyor.
        Assert.NotEmpty(Round(AgeBand.Fidan).Items);
    }

    [Fact]
    public void The_basket_never_leaves_the_screen()
    {
        var round = Round(AgeBand.Fidan);
        var half = round.BasketWidth / 2f;

        round.MoveBasketTo(-5f);
        Assert.Equal(half, round.BasketX, 5);

        round.MoveBasketTo(5f);
        Assert.Equal(1f - half, round.BasketX, 5);

        round.MoveBasketTo(0.5f);
        Assert.Equal(0.5f, round.BasketX, 5);
    }

    [Fact]
    public void An_item_over_the_basket_is_caught()
    {
        var round = Round(AgeBand.Fidan);
        var item = round.Items[0];

        round.MoveBasketTo(item.X);

        while (round.Items.Contains(item) && round.Elapsed < TimeSpan.FromSeconds(20))
        {
            round.MoveBasketTo(item.X);
            round.Advance(Frame);
        }

        Assert.Equal(1, round.Caught);
        Assert.Equal(0, round.Missed);
        Assert.Contains(round.LastEvents, e => e.ItemId == item.Id && e.Caught);
    }

    [Fact]
    public void An_item_away_from_the_basket_falls_through()
    {
        var round = Round(AgeBand.Mese);
        var item = round.Items[0];

        // Sepeti nesneden olabildiğince uzağa çek.
        round.MoveBasketTo(item.X > 0.5f ? 0f : 1f);

        var basketX = round.BasketX;
        while (round.Items.Contains(item) && round.Elapsed < TimeSpan.FromSeconds(20))
        {
            round.MoveBasketTo(basketX);
            round.Advance(Frame);
        }

        Assert.Equal(0, round.Caught);
        Assert.Equal(1, round.Missed);
        Assert.Contains(round.LastEvents, e => e.ItemId == item.Id && !e.Caught);
    }

    [Fact]
    public void A_missed_item_stays_visible_until_it_leaves_the_screen()
    {
        // Havada yok olsaydı çocuk neyi kaçırdığını göremezdi.
        var round = Round(AgeBand.Fidan);
        var item = round.Items[0];

        round.MoveBasketTo(item.X > 0.5f ? 0f : 1f);
        var basketX = round.BasketX;

        while (item.Y < BasketCatchRound.CatchLine + 0.05f)
        {
            round.MoveBasketTo(basketX);
            round.Advance(Frame);
        }

        Assert.Contains(item, round.Items);
        Assert.Equal(0, round.Missed);
    }

    [Fact]
    public void The_basket_arriving_late_does_not_rescue_an_item()
    {
        // Yakalama sepetin ağzını geçerken bir kez sınanıyor; altından geçen
        // nesne sepet oraya sonradan gelince yakalanmıyor.
        var round = Round(AgeBand.Fidan);
        var item = round.Items[0];

        round.MoveBasketTo(item.X > 0.5f ? 0f : 1f);
        var away = round.BasketX;

        while (round.Items.Contains(item) && item.Y < BasketCatchRound.CatchLine + 0.02f)
        {
            round.MoveBasketTo(away);
            round.Advance(Frame);
        }

        // Nesne ağzı geçti; şimdi sepeti tam altına götür.
        while (round.Items.Contains(item))
        {
            round.MoveBasketTo(item.X);
            round.Advance(Frame);
        }

        Assert.Equal(0, round.Caught);
        Assert.Equal(1, round.Missed);
    }

    [Fact]
    public void Only_the_oldest_band_pays_for_a_miss()
    {
        foreach (var band in new[] { AgeBand.Filiz, AgeBand.Fidan })
        {
            var round = Round(band);
            round.MoveBasketTo(0f);
            RunFrames(round, 600);

            Assert.True(round.Missed > 0, $"{band}: hiç nesne kaçmadı, test bir şey ölçmüyor");
            Assert.Equal(0, round.Mistakes);
        }

        var mese = Round(AgeBand.Mese);
        mese.MoveBasketTo(0f);
        RunFrames(mese, 600);

        Assert.True(mese.Missed > 0);
        Assert.Equal(mese.Missed, mese.Mistakes);
    }

    [Fact]
    public void Items_stay_inside_the_screen_while_they_drift()
    {
        // Savrulma genliği kenar payına giriyor, yoksa nesne ekranın dışına
        // salınıp geri geliyor.
        for (var seed = 0; seed < 20; seed++)
        {
            var round = BasketCatchRound.ForBand(AgeBand.Mese, new Random(seed));

            for (var frame = 0; frame < 900; frame++)
            {
                round.Advance(Frame);

                foreach (var item in round.Items)
                {
                    Assert.InRange(item.X, 0f, 1f);
                }
            }
        }
    }

    [Fact]
    public void The_screen_never_holds_more_items_than_the_band_allows()
    {
        var round = Round(AgeBand.Mese);

        for (var frame = 0; frame < 1200; frame++)
        {
            round.Advance(Frame);
            Assert.True(round.Items.Count <= round.MaxItems);
        }
    }

    [Fact]
    public void Catching_the_goal_completes_the_round()
    {
        var round = Round(AgeBand.Fidan);

        PlayPerfectly(round);

        Assert.True(round.IsComplete);
        Assert.Equal(round.Goal, round.Caught);
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void A_finished_round_stops_moving()
    {
        var round = Round(AgeBand.Filiz);
        PlayPerfectly(round);

        var elapsed = round.Elapsed;
        var caught = round.Caught;

        RunFrames(round, 120);

        Assert.Equal(elapsed, round.Elapsed);
        Assert.Equal(caught, round.Caught);
        Assert.Empty(round.LastEvents);
    }

    [Fact]
    public void Events_only_describe_the_frame_that_just_ran()
    {
        var round = Round(AgeBand.Fidan);
        var item = round.Items[0];

        round.MoveBasketTo(item.X);
        while (round.Items.Contains(item))
        {
            round.MoveBasketTo(item.X);
            round.Advance(Frame);
        }

        Assert.Contains(round.LastEvents, e => e.ItemId == item.Id && e.Caught);

        round.Advance(Frame);
        Assert.DoesNotContain(round.LastEvents, e => e.ItemId == item.Id);
    }

    [Fact]
    public void The_same_seed_produces_the_same_round()
    {
        var a = Round(AgeBand.Mese, seed: 17);
        var b = Round(AgeBand.Mese, seed: 17);

        Assert.Equal(a.Items[0].Kind, b.Items[0].Kind);
        Assert.Equal(a.Items[0].Hue, b.Items[0].Hue);
        Assert.Equal(a.Items[0].X, b.Items[0].X, 5);
    }
}
