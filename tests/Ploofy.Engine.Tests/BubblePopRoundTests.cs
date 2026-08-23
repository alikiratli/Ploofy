using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

public class BubblePopRoundTests
{
    private static BubblePopRound Round(AgeBand band, int seed = 11) =>
        BubblePopRound.ForBand(band, new Random(seed));

    /// <summary>Saati kare kare ilerletir — gerçek oyun döngüsünün testteki karşılığı.</summary>
    private static void Run(BubblePopRound round, double seconds, double frame = 1.0 / 60)
    {
        for (var t = 0d; t < seconds; t += frame)
        {
            round.Advance(TimeSpan.FromSeconds(frame));
        }
    }

    [Fact]
    public void The_screen_is_never_empty_at_the_start()
    {
        // Boş ekrana bakarak beklemek bu yaşta "oyun başlamadı" demek.
        Assert.NotEmpty(Round(AgeBand.Filiz).Bubbles);
    }

    [Fact]
    public void Filiz_has_no_target_colour_so_every_bubble_counts()
    {
        var round = Round(AgeBand.Filiz);
        Assert.Null(round.TargetHue);

        var bubble = round.Bubbles[^1];
        Assert.Equal(PopOutcome.Popped, round.PopAt(bubble.X, bubble.Y));
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void Older_bands_get_a_target_colour()
    {
        Assert.NotNull(Round(AgeBand.Fidan).TargetHue);
        Assert.NotNull(Round(AgeBand.Mese).TargetHue);
    }

    [Fact]
    public void Popping_the_wrong_colour_counts_as_a_mistake_and_leaves_the_bubble()
    {
        var round = Round(AgeBand.Mese);
        var target = round.TargetHue!.Value;

        Run(round, 2);

        var wrong = round.Bubbles.LastOrDefault(b => b.Hue != target);
        Assert.NotNull(wrong);

        var before = round.Bubbles.Count;
        Assert.Equal(PopOutcome.WrongColor, round.PopAt(wrong.X, wrong.Y));

        Assert.Equal(1, round.Mistakes);
        Assert.Equal(0, round.Popped);
        // Yanlış balon patlamıyor: patlasaydı "yanlış" da bir ödül olurdu.
        Assert.Equal(before, round.Bubbles.Count);
    }

    [Fact]
    public void Touching_empty_space_is_a_miss_not_a_mistake()
    {
        var round = Round(AgeBand.Mese);

        // Balonların hiçbirinin bulunmadığı bir nokta.
        Assert.Equal(PopOutcome.Miss, round.PopAt(-0.5f, -0.5f));
        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void The_touch_target_is_wider_than_the_drawn_bubble()
    {
        // Küçük parmak balonun kenarına değdiğinde de patlamalı.
        var round = Round(AgeBand.Filiz);
        var bubble = round.Bubbles[^1];

        var justOutside = bubble.X + (bubble.Radius * 1.2f);
        Assert.Equal(PopOutcome.Popped, round.PopAt(justOutside, bubble.Y));
    }

    [Fact]
    public void Bubbles_rise_and_leave_the_screen()
    {
        var round = Round(AgeBand.Filiz);
        var tracked = round.Bubbles[0];
        var startY = tracked.Y;

        Run(round, 0.5);

        Assert.True(tracked.Y < startY);
        Assert.DoesNotContain(round.Bubbles, b => b.Y + b.Radius < 0f);
    }

    [Fact]
    public void Escaped_bubbles_are_not_counted_as_mistakes()
    {
        // Iskalamak ile yanlış renge dokunmak farklı şeyler.
        var round = Round(AgeBand.Mese);
        Run(round, 12);

        Assert.Equal(0, round.Mistakes);
    }

    [Fact]
    public void The_screen_never_holds_more_than_the_band_allows()
    {
        var round = Round(AgeBand.Mese);
        Run(round, 20);

        Assert.True(round.Bubbles.Count <= round.MaxBubbles);
    }

    [Fact]
    public void The_target_colour_always_comes_back_around()
    {
        // Şansın oyunu kilitlemesi, çocuk doğru oynarken ilerleyememesi demek.
        var round = Round(AgeBand.Fidan);
        var target = round.TargetHue!.Value;

        for (var i = 0; i < 40; i++)
        {
            Run(round, 0.75);
            Assert.Contains(round.Bubbles, b => b.Hue == target);
        }
    }

    [Fact]
    public void Reaching_the_goal_completes_the_round()
    {
        var round = Round(AgeBand.Filiz);

        while (!round.IsComplete)
        {
            round.Advance(TimeSpan.FromSeconds(1.0 / 60));
            var bubble = round.Bubbles.LastOrDefault();
            if (bubble is not null)
            {
                round.PopAt(bubble.X, bubble.Y);
            }
        }

        Assert.True(round.IsComplete);
        Assert.True(round.IsOver);
        Assert.Equal(round.Goal, round.Popped);
    }

    [Fact]
    public void Only_the_oldest_band_can_run_out_of_time()
    {
        Assert.Null(Round(AgeBand.Filiz).TimeLimit);
        Assert.Null(Round(AgeBand.Fidan).TimeLimit);

        var mese = Round(AgeBand.Mese);
        Assert.NotNull(mese.TimeLimit);

        Run(mese, mese.TimeLimit!.Value.TotalSeconds + 1);

        Assert.True(mese.IsTimeUp);
        Assert.False(mese.IsComplete);
        Assert.Equal(TimeSpan.Zero, mese.Remaining);
    }

    [Fact]
    public void A_finished_round_stops_responding()
    {
        var round = Round(AgeBand.Mese);
        Run(round, round.TimeLimit!.Value.TotalSeconds + 1);

        var before = round.Popped;
        Assert.Equal(PopOutcome.Miss, round.PopAt(0.5f, 0.5f));
        Assert.Equal(before, round.Popped);
    }

    [Theory]
    [InlineData(AgeBand.Filiz)]
    [InlineData(AgeBand.Fidan)]
    [InlineData(AgeBand.Mese)]
    public void Bubbles_stay_inside_the_screen(AgeBand band)
    {
        var round = Round(band);
        Run(round, 15);

        Assert.All(round.Bubbles, b =>
        {
            Assert.InRange(b.X, -0.1f, 1.1f);
        });
    }

    [Fact]
    public void Younger_bands_get_bigger_slower_bubbles()
    {
        var filiz = Round(AgeBand.Filiz);
        var mese = Round(AgeBand.Mese);

        Assert.True(filiz.BubbleRadius > mese.BubbleRadius);
        Assert.True(filiz.RiseSpeed < mese.RiseSpeed);
        Assert.True(filiz.MaxBubbles < mese.MaxBubbles);
    }

    [Fact]
    public void The_same_seed_produces_the_same_round()
    {
        var a = Round(AgeBand.Fidan, seed: 3);
        var b = Round(AgeBand.Fidan, seed: 3);

        Assert.Equal(a.TargetHue, b.TargetHue);
        Assert.Equal(
            a.Bubbles.Select(x => (x.Hue, x.Radius)),
            b.Bubbles.Select(x => (x.Hue, x.Radius)));
    }
}
