using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

public class MazeTraceRoundTests
{
    private static MazeTraceRound Round(AgeBand band, int seed = 6) =>
        MazeTraceRound.ForBand(band, new Random(seed));

    /// <summary>Yolu nokta nokta, tam üstünden takip eder.</summary>
    private static TraceOutcome TraceLevel(MazeTraceRound round)
    {
        var points = round.Points.ToList();
        Assert.Equal(TraceOutcome.Started, round.Begin(points[0].X, points[0].Y));

        var outcome = TraceOutcome.Started;
        foreach (var point in points)
        {
            outcome = round.MoveTo(point.X, point.Y);
            if (outcome == TraceOutcome.LevelComplete)
            {
                break;
            }

            Assert.Equal(TraceOutcome.Advanced, outcome);
        }

        return outcome;
    }

    private static void PlayThrough(MazeTraceRound round)
    {
        while (!round.IsComplete)
        {
            Assert.Equal(TraceOutcome.LevelComplete, TraceLevel(round));
        }
    }

    private static float Distance(PathPoint a, PathPoint b)
    {
        var dx = a.X - b.X;
        var dy = a.Y - b.Y;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    [Theory]
    [InlineData(AgeBand.Filiz, 3)]
    [InlineData(AgeBand.Fidan, 4)]
    [InlineData(AgeBand.Mese, 5)]
    public void The_number_of_paths_scales_with_the_band(AgeBand band, int levels) =>
        Assert.Equal(levels, Round(band).Total);

    [Fact]
    public void The_path_gets_thinner_with_age()
    {
        // Zorluğun yarısı burada: Filiz'de yol kalın bir şerit, Meşe'de
        // ince bir çizgi gibi davranıyor.
        Assert.True(Round(AgeBand.Filiz).Tolerance > Round(AgeBand.Fidan).Tolerance);
        Assert.True(Round(AgeBand.Fidan).Tolerance > Round(AgeBand.Mese).Tolerance);
    }

    [Fact]
    public void Putting_the_finger_down_is_easier_than_staying_on()
    {
        // İnce bir çizginin tam üstüne parmak indirmek, onu takip etmekten
        // zor: dokunmadan önce parmağın altını göremiyorsun.
        var round = Round(AgeBand.Mese);
        Assert.True(round.GrabTolerance > round.Tolerance);
    }

    [Fact]
    public void Every_path_stays_inside_the_square()
    {
        for (var seed = 0; seed < 60; seed++)
        {
            foreach (var band in Enum.GetValues<AgeBand>())
            {
                var round = MazeTraceRound.ForBand(band, new Random(seed));

                while (!round.IsComplete)
                {
                    Assert.All(round.Points, point =>
                    {
                        Assert.InRange(point.X, 0f, 1f);
                        Assert.InRange(point.Y, 0f, 1f);
                    });

                    TraceLevel(round);
                }
            }
        }
    }

    [Fact]
    public void The_path_never_jumps_between_samples()
    {
        // Ardışık noktalar toleranstan yakın olmalı, yoksa yolun üstünden
        // giden bir parmak bile boşluğa düşüp yoldan çıkmış sayılır.
        for (var seed = 0; seed < 40; seed++)
        {
            var round = MazeTraceRound.ForBand(AgeBand.Mese, new Random(seed));

            while (!round.IsComplete)
            {
                for (var i = 1; i < round.Points.Count; i++)
                {
                    Assert.True(
                        Distance(round.Points[i - 1], round.Points[i]) < round.Tolerance,
                        $"seed {seed}, {round.Shape}: {i}. noktada sıçrama var");
                }

                TraceLevel(round);
            }
        }
    }

    [Fact]
    public void The_two_ends_are_far_enough_apart_to_be_a_path()
    {
        for (var seed = 0; seed < 40; seed++)
        {
            var round = MazeTraceRound.ForBand(AgeBand.Fidan, new Random(seed));
            Assert.True(Distance(round.Start, round.Goal) > 0.4f);
        }
    }

    [Fact]
    public void A_touch_away_from_the_path_does_nothing()
    {
        var round = Round(AgeBand.Fidan);

        // Yolun başından uzak bir köşe.
        var corner = round.Start.X < 0.5f ? 1f : 0f;
        Assert.Equal(TraceOutcome.Ignored, round.Begin(corner, corner));

        Assert.False(round.IsTracing);
        Assert.Equal(0, round.Slips);
        Assert.Equal(0f, round.Progress, 5);
    }

    [Fact]
    public void Moving_without_touching_first_is_ignored()
    {
        var round = Round(AgeBand.Fidan);
        var second = round.Points[1];

        Assert.Equal(TraceOutcome.Ignored, round.MoveTo(second.X, second.Y));
        Assert.Equal(0f, round.Progress, 5);
    }

    [Fact]
    public void Following_the_path_advances_and_finishes_it()
    {
        var round = Round(AgeBand.Fidan);

        Assert.Equal(TraceOutcome.LevelComplete, TraceLevel(round));

        Assert.Equal(1, round.Completed);
        Assert.Equal(0, round.Slips);
        Assert.False(round.IsTracing);
    }

    [Fact]
    public void Leaving_the_path_counts_once_per_excursion()
    {
        // Her karede saymak, yolun dışında duran bir parmağı saniyede altmış
        // hataya çeviriyordu.
        var round = Round(AgeBand.Mese);
        var start = round.Start;

        round.Begin(start.X, start.Y);

        var offX = Math.Clamp(start.X + (round.Tolerance * 6f), 0f, 1f);
        var offY = Math.Clamp(start.Y + (round.Tolerance * 6f), 0f, 1f);

        Assert.Equal(TraceOutcome.Slipped, round.MoveTo(offX, offY));
        Assert.Equal(TraceOutcome.Slipped, round.MoveTo(offX, offY));
        Assert.Equal(TraceOutcome.Slipped, round.MoveTo(offX, offY));

        Assert.Equal(1, round.Slips);
        Assert.True(round.IsOffPath);
    }

    [Fact]
    public void Coming_back_to_the_path_resumes_the_trace()
    {
        var round = Round(AgeBand.Mese);
        var points = round.Points.ToList();

        round.Begin(points[0].X, points[0].Y);
        round.MoveTo(points[4].X, points[4].Y);
        var reached = round.Progress;

        var offX = Math.Clamp(points[4].X + (round.Tolerance * 6f), 0f, 1f);
        var offY = Math.Clamp(points[4].Y + (round.Tolerance * 6f), 0f, 1f);
        Assert.Equal(TraceOutcome.Slipped, round.MoveTo(offX, offY));

        // Yola dönünce kaldığı yerden devam ediyor, ilerleme silinmiyor.
        Assert.Equal(TraceOutcome.Advanced, round.MoveTo(points[5].X, points[5].Y));
        Assert.False(round.IsOffPath);
        Assert.True(round.Progress > reached);
        Assert.Equal(1, round.Slips);
    }

    [Fact]
    public void Sliding_back_along_the_path_is_not_a_slip()
    {
        // Cihazda görüldü: dar bir geri pay bırakıldığında köşede geriye
        // kayan parmak yoldan çıkmış sayılıyor ve Meşe'de bu bir hata puanı.
        var round = Round(AgeBand.Mese);
        var points = round.Points.ToList();

        round.Begin(points[0].X, points[0].Y);
        for (var i = 0; i <= 40; i++)
        {
            round.MoveTo(points[i].X, points[i].Y);
        }

        var reached = round.Progress;

        // Yolun ta başına kadar geri git — hepsi yolun üstünde.
        for (var i = 40; i >= 0; i--)
        {
            Assert.Equal(TraceOutcome.Advanced, round.MoveTo(points[i].X, points[i].Y));
        }

        Assert.Equal(0, round.Slips);
        Assert.False(round.IsOffPath);
        Assert.Equal(reached, round.Progress, 5);
    }

    [Fact]
    public void Progress_never_goes_backwards()
    {
        // Titreyen parmak kazanılanı geri almıyor.
        var round = Round(AgeBand.Fidan);
        var points = round.Points.ToList();

        round.Begin(points[0].X, points[0].Y);
        round.MoveTo(points[6].X, points[6].Y);
        var reached = round.Progress;

        Assert.Equal(TraceOutcome.Advanced, round.MoveTo(points[5].X, points[5].Y));
        Assert.Equal(reached, round.Progress, 5);
    }

    [Fact]
    public void Lifting_the_finger_keeps_the_progress()
    {
        // Küçük çocuk parmağını uzun süre ekranda tutamıyor; kalkmayı
        // cezalandırmak oyunu bitirilemez yapardı.
        var round = Round(AgeBand.Fidan);
        var points = round.Points.ToList();

        round.Begin(points[0].X, points[0].Y);
        round.MoveTo(points[8].X, points[8].Y);
        var reached = round.Progress;

        round.Release();

        Assert.False(round.IsTracing);
        Assert.Equal(reached, round.Progress, 5);
        Assert.Equal(0, round.Slips);
    }

    [Fact]
    public void The_finger_goes_back_on_at_the_head_not_at_the_start()
    {
        var round = Round(AgeBand.Fidan);
        var points = round.Points.ToList();

        round.Begin(points[0].X, points[0].Y);
        for (var i = 0; i <= 40; i++)
        {
            round.MoveTo(points[i].X, points[i].Y);
        }

        round.Release();

        // Başlangıç artık kabul edilmiyor: oradan devam etmek yolun
        // yarısını boşuna çizmek olurdu.
        Assert.Equal(TraceOutcome.Ignored, round.Begin(points[0].X, points[0].Y));

        var head = round.Head;
        Assert.Equal(TraceOutcome.Started, round.Begin(head.X, head.Y));
    }

    [Fact]
    public void The_finger_cannot_skip_ahead_to_a_later_stretch()
    {
        // Penceresiz arama, yola yakın geçen ileri bir bölüme atlamayı
        // mümkün kılıyordu.
        var round = Round(AgeBand.Mese);
        var points = round.Points.ToList();

        round.Begin(points[0].X, points[0].Y);

        var far = points[^3];
        Assert.Equal(TraceOutcome.Slipped, round.MoveTo(far.X, far.Y));
        Assert.True(round.Progress < 0.5f);
    }

    [Fact]
    public void Only_the_oldest_band_pays_for_leaving_the_path()
    {
        foreach (var band in new[] { AgeBand.Filiz, AgeBand.Fidan })
        {
            var round = Round(band);
            var start = round.Start;
            round.Begin(start.X, start.Y);
            round.MoveTo(
                Math.Clamp(start.X + (round.Tolerance * 6f), 0f, 1f),
                Math.Clamp(start.Y + (round.Tolerance * 6f), 0f, 1f));

            Assert.Equal(1, round.Slips);
            Assert.Equal(0, round.Mistakes);
        }

        var mese = Round(AgeBand.Mese);
        var meseStart = mese.Start;
        mese.Begin(meseStart.X, meseStart.Y);
        mese.MoveTo(
            Math.Clamp(meseStart.X + (mese.Tolerance * 6f), 0f, 1f),
            Math.Clamp(meseStart.Y + (mese.Tolerance * 6f), 0f, 1f));

        Assert.Equal(1, mese.Slips);
        Assert.Equal(1, mese.Mistakes);
    }

    [Fact]
    public void Finishing_a_path_hands_over_a_fresh_one()
    {
        var round = Round(AgeBand.Mese);
        var first = round.Points.ToList();

        TraceLevel(round);

        Assert.Equal(1, round.Completed);
        Assert.Equal(0f, round.Progress, 5);
        Assert.False(round.IsTracing);
        Assert.NotEqual(first, round.Points);
    }

    [Fact]
    public void Tracing_every_path_completes_the_round()
    {
        var round = Round(AgeBand.Fidan);

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

        var head = round.Head;
        Assert.Equal(TraceOutcome.Ignored, round.Begin(head.X, head.Y));
    }

    [Fact]
    public void The_oldest_band_never_gets_a_straight_line()
    {
        // O bantta düz bir yolu takip etmek beceri değil.
        for (var seed = 0; seed < 40; seed++)
        {
            var round = MazeTraceRound.ForBand(AgeBand.Mese, new Random(seed));

            while (!round.IsComplete)
            {
                Assert.NotEqual(PathShape.Straight, round.Shape);
                TraceLevel(round);
            }
        }
    }

    [Fact]
    public void The_same_seed_produces_the_same_path()
    {
        var a = Round(AgeBand.Fidan, seed: 31);
        var b = Round(AgeBand.Fidan, seed: 31);

        Assert.Equal(a.Shape, b.Shape);
        Assert.Equal(a.Points, b.Points);
    }
}
