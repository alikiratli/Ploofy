using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

/// <summary>
/// Harf şekilleri ekrana bakılarak doğrulanamıyor — ekran yok. Onun yerine
/// biçimin bozulduğunda kırılacak sayısal özellikleri sınanıyor: kutunun
/// dışına taşma, kopuk darbe, eşit olmayan örnekleme, eksik harf.
/// </summary>
public class GlyphShapesTests
{
    /// <summary>Türkçe alfabe (29 harf) — Q, W, X yok.</summary>
    private static readonly string[] TurkishUpper =
    [
        "A", "B", "C", "Ç", "D", "E", "F", "G", "Ğ", "H", "I", "İ", "J", "K", "L",
        "M", "N", "O", "Ö", "P", "R", "S", "Ş", "T", "U", "Ü", "V", "Y", "Z",
    ];

    private static readonly string[] EnglishUpper =
    [
        "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
        "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
    ];

    private static IEnumerable<IReadOnlyList<PathPoint>> AllPaths() =>
        GlyphShapes.Characters
            .Select(c => GlyphShapes.Find(c)!)
            .SelectMany(g => g.Strokes.Concat(g.Marks));

    private static float Length(IReadOnlyList<PathPoint> path)
    {
        var total = 0f;
        for (var i = 1; i < path.Count; i++)
        {
            var dx = path[i].X - path[i - 1].X;
            var dy = path[i].Y - path[i - 1].Y;
            total += MathF.Sqrt((dx * dx) + (dy * dy));
        }

        return total;
    }

    [Fact]
    public void Every_letter_of_all_three_alphabets_can_be_written()
    {
        // Bir harfin yazım yolu yoksa oyun o harfi sessizce eliyor ve çocuk
        // kendi alfabesinin bir harfini hiç görmüyor.
        foreach (var letter in TurkishUpper.Concat(EnglishUpper).Concat(["Ä", "Ö", "Ü"]))
        {
            Assert.True(GlyphShapes.Has(letter), $"{letter} yazılamıyor");
        }
    }

    [Fact]
    public void Every_digit_can_be_written()
    {
        foreach (var digit in Enumerable.Range(0, 10).Select(n => n.ToString()))
        {
            Assert.True(GlyphShapes.Has(digit), $"{digit} yazılamıyor");
        }
    }

    [Fact]
    public void German_sharp_s_is_deliberately_absent()
    {
        // Sözcük başında hiç bulunmuyor ve bu yaşta öğretilmiyor. Kasıtlı
        // olduğunu söyleyen tek yer bu test.
        Assert.False(GlyphShapes.Has("ß"));
    }

    [Fact]
    public void Nothing_is_drawn_outside_the_square()
    {
        // Taşan bir nokta ekranda kırpılır ve harfin bir parçası kaybolur.
        foreach (var path in AllPaths())
        {
            Assert.All(path, p =>
            {
                Assert.InRange(p.X, 0f, 1f);
                Assert.InRange(p.Y, 0f, 1f);
            });
        }
    }

    [Fact]
    public void No_stroke_is_a_single_point()
    {
        foreach (var path in AllPaths())
        {
            Assert.True(path.Count >= 2);
            Assert.True(Length(path) > 0.01f);
        }
    }

    [Fact]
    public void Every_traced_stroke_is_long_enough_to_be_traced()
    {
        // Süsler kısa olabilir (nokta, kuyruk) ama takip edilen bir darbe
        // parmakla çizilebilecek kadar uzun olmalı.
        foreach (var character in GlyphShapes.Characters)
        {
            var glyph = GlyphShapes.Find(character)!;
            Assert.All(glyph.Strokes, stroke =>
                Assert.True(Length(stroke) > 0.15f, $"{character}: darbe çok kısa"));
        }
    }

    [Fact]
    public void Points_are_spaced_evenly_along_every_stroke()
    {
        // Eşit aralık şart: ilerleme nokta indisinden sayılıyor. Sık
        // örneklenen bir bölge yolun gereğinden büyük bir parçası sayılır ve
        // L'nin dikeyini bitiren çocuk harfi yarılanmış görür.
        foreach (var path in AllPaths())
        {
            var steps = new List<float>();
            for (var i = 1; i < path.Count; i++)
            {
                var dx = path[i].X - path[i - 1].X;
                var dy = path[i].Y - path[i - 1].Y;
                steps.Add(MathF.Sqrt((dx * dx) + (dy * dy)));
            }

            // Köşeyi kesen adım kirişten kısa çıkıyor ve bu zararsız.
            // Tehlikeli olan uzun adım: seyrek bir bölge ilerlemeyi
            // sıçratıyor ve parmağın bir parçayı atlamasına yer açıyor.
            steps.Sort();
            var median = steps[steps.Count / 2];

            Assert.True(steps[^1] < median * 1.6f);
        }
    }

    [Fact]
    public void An_accented_letter_shares_the_body_of_its_base_letter()
    {
        // Ç ile C'nin yazımı arasındaki tek fark kuyruk. Gövde paylaşılmazsa
        // C düzeltildiğinde Ç eski hâliyle kalır.
        Assert.Equal(GlyphShapes.Find("C")!.Strokes, GlyphShapes.Find("Ç")!.Strokes);
        Assert.Equal(GlyphShapes.Find("O")!.Strokes, GlyphShapes.Find("Ö")!.Strokes);
        Assert.Equal(GlyphShapes.Find("I")!.Strokes, GlyphShapes.Find("İ")!.Strokes);
        Assert.Equal(GlyphShapes.Find("S")!.Strokes, GlyphShapes.Find("Ş")!.Strokes);
    }

    [Fact]
    public void The_accent_itself_is_never_traced()
    {
        // Bir noktayı "takip etmek" diye bir şey yok: parmağın yeri var,
        // yönü yok.
        foreach (var character in new[] { "Ç", "Ğ", "İ", "Ö", "Ü", "Ş", "Ä" })
        {
            var glyph = GlyphShapes.Find(character)!;
            Assert.NotEmpty(glyph.Marks);
        }

        Assert.Empty(GlyphShapes.Find("A")!.Marks);
    }

    [Fact]
    public void Letters_that_need_more_than_one_stroke_have_them_in_teaching_order()
    {
        // E: önce dikey, sonra üst, orta, alt. Sıra keyfi değil — yanlış
        // sırayla yazmayı öğrenen çocuk bunu sonradan zor bırakıyor.
        var e = GlyphShapes.Find("E")!;
        Assert.Equal(4, e.Strokes.Count);

        var stem = e.Strokes[0];
        Assert.Equal(stem[0].X, stem[^1].X, 3);
        Assert.True(stem[0].Y < stem[^1].Y);

        // Kalan üçü yatay ve yukarıdan aşağı sıralı.
        var bars = e.Strokes.Skip(1).ToList();
        Assert.All(bars, bar => Assert.Equal(bar[0].Y, bar[^1].Y, 3));
        Assert.True(bars[0][0].Y < bars[1][0].Y);
        Assert.True(bars[1][0].Y < bars[2][0].Y);
    }

    [Fact]
    public void A_closed_shape_comes_back_to_where_it_started()
    {
        // O ve 0 kapalı; başlangıcına dönmeyen bir halka ekranda çentikli
        // görünür.
        foreach (var character in new[] { "O", "0" })
        {
            var ring = GlyphShapes.Find(character)!.Strokes[0];
            var dx = ring[0].X - ring[^1].X;
            var dy = ring[0].Y - ring[^1].Y;

            Assert.True(MathF.Sqrt((dx * dx) + (dy * dy)) < 0.02f);
        }
    }
}

public class LetterTraceRoundTests
{
    private static readonly string[] Pool =
    [
        "A", "B", "C", "T", "L", "O", "1", "4", "7",
    ];

    private static LetterTraceRound Round(AgeBand band, int seed = 4) =>
        LetterTraceRound.ForBand(band, Pool, new Random(seed));

    /// <summary>Sıradaki darbeyi nokta nokta, tam üstünden çizer.</summary>
    private static TraceOutcome TraceStroke(LetterTraceRound round)
    {
        var stroke = round.ActiveStroke!;
        var points = stroke.Points.ToList();

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

    /// <summary>
    /// İşaretin bütün darbelerini çizer.
    /// </summary>
    /// <remarks>
    /// Ölçüt <c>GlyphComplete</c> değil bitirilen sayısı: bayrak bir sonraki
    /// <c>MoveTo</c>'ya kadar açık kalıyor, yani onu ölçüt alan bir döngü
    /// ikinci harfe hiç başlamıyor.
    /// </remarks>
    private static void WriteGlyph(LetterTraceRound round)
    {
        var before = round.Completed;
        while (round.Completed == before)
        {
            Assert.Equal(TraceOutcome.LevelComplete, TraceStroke(round));
        }
    }

    private static void PlayThrough(LetterTraceRound round)
    {
        while (!round.IsComplete)
        {
            WriteGlyph(round);
        }
    }

    [Theory]
    [InlineData(AgeBand.Fidan, 3)]
    [InlineData(AgeBand.Mese, 5)]
    public void The_number_of_glyphs_scales_with_the_band(AgeBand band, int glyphs) =>
        Assert.Equal(glyphs, Round(band).Total);

    [Fact]
    public void The_stroke_gets_thinner_with_age()
    {
        Assert.True(Round(AgeBand.Fidan).Tolerance > Round(AgeBand.Mese).Tolerance);
    }

    [Fact]
    public void The_stroke_is_wider_than_in_maze_trace()
    {
        // Harf darbeleri kısa ve köşeli, yol kıvrımları uzun ve yumuşak. Aynı
        // toleransta harf belirgin biçimde zorlaşıyor ve zorluk beceriden
        // değil biçimden geliyor.
        Assert.True(
            LetterTraceTuning.Tolerance.For(AgeBand.Mese) >
            MazeTraceTuning.Tolerance.For(AgeBand.Mese));
    }

    [Fact]
    public void Characters_without_a_writing_path_are_dropped()
    {
        // ß'nin yazım yolu yok; havuza girse bile tura giremez.
        var round = LetterTraceRound.ForBand(AgeBand.Fidan, ["A", "ß", "O"], new Random(1));

        PlayThrough(round);
        Assert.True(round.IsComplete);
    }

    [Fact]
    public void A_pool_with_nothing_writable_is_a_mistake_not_an_empty_round()
    {
        // Sessizce boş bir tur açmak, çocuğa hiçbir şey yapmadan biten bir
        // oyun göstermek demek.
        Assert.Throws<ArgumentException>(() =>
            LetterTraceRound.ForBand(AgeBand.Fidan, ["ß", "@"], new Random(1)));
    }

    [Fact]
    public void The_strokes_have_to_be_written_in_order()
    {
        // T: önce üst çizgi, sonra dikey. İkinciye önce dokunmak hiçbir şey
        // yapmıyor.
        var round = LetterTraceRound.ForBand(AgeBand.Mese, ["T"], new Random(1));

        var second = round.Strokes[1].Points[0];
        Assert.Equal(TraceOutcome.Ignored, round.Begin(second.X, second.Y));
        Assert.Equal(0, round.StrokeIndex);
    }

    [Fact]
    public void Finishing_a_stroke_hands_over_the_next_one()
    {
        var round = LetterTraceRound.ForBand(AgeBand.Mese, ["T"], new Random(1));

        Assert.Equal(TraceOutcome.LevelComplete, TraceStroke(round));

        Assert.Equal(1, round.StrokeIndex);
        Assert.False(round.GlyphComplete);
        Assert.Equal(0, round.Completed);
    }

    [Fact]
    public void The_glyph_is_only_complete_after_its_last_stroke()
    {
        // Arayüz kutlamayı buna bakarak yapıyor; her darbede kutlamak
        // dört darbeli bir E'yi dört kez bitirilmiş gösterirdi.
        var round = LetterTraceRound.ForBand(AgeBand.Mese, ["T"], new Random(1));

        TraceStroke(round);
        Assert.False(round.GlyphComplete);

        TraceStroke(round);
        Assert.True(round.GlyphComplete);
        Assert.Equal(1, round.Completed);
    }

    [Fact]
    public void A_single_stroke_letter_is_done_in_one_go()
    {
        var round = LetterTraceRound.ForBand(AgeBand.Mese, ["O"], new Random(1));

        Assert.Single(round.Strokes);
        TraceStroke(round);

        Assert.True(round.GlyphComplete);
        Assert.Equal(1, round.Completed);
    }

    [Fact]
    public void Writing_every_glyph_completes_the_round()
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
        var round = Round(AgeBand.Fidan);
        PlayThrough(round);

        Assert.Equal(TraceOutcome.Ignored, round.Begin(0.5f, 0.5f));
    }

    [Fact]
    public void Leaving_the_stroke_counts_once_per_excursion()
    {
        var round = LetterTraceRound.ForBand(AgeBand.Mese, ["I"], new Random(1));
        var points = round.ActiveStroke!.Points.ToList();

        round.Begin(points[0].X, points[0].Y);
        round.MoveTo(points[1].X, points[1].Y);

        // Uzağa çıkıp orada birkaç kare durmak tek hata.
        Assert.Equal(TraceOutcome.Slipped, round.MoveTo(0.95f, 0.5f));
        Assert.Equal(TraceOutcome.Slipped, round.MoveTo(0.96f, 0.5f));
        Assert.Equal(1, round.Slips);
    }

    [Fact]
    public void Only_the_oldest_band_pays_for_leaving_the_stroke()
    {
        // Fidan'da amaç harfin şeklini tanımak; elin titremesini
        // cezalandırmak yazmayı sevdirmenin tersi.
        foreach (var band in new[] { AgeBand.Fidan, AgeBand.Mese })
        {
            var round = LetterTraceRound.ForBand(band, ["I"], new Random(1));
            var points = round.ActiveStroke!.Points.ToList();

            round.Begin(points[0].X, points[0].Y);
            round.MoveTo(0.95f, 0.5f);

            Assert.Equal(1, round.Slips);
            Assert.Equal(band == AgeBand.Mese ? 1 : 0, round.Mistakes);
        }
    }

    [Fact]
    public void Slips_survive_moving_on_to_the_next_glyph()
    {
        // Hata sayısı yıldıza gidiyor; harf değişince sıfırlanması, üçüncü
        // harfte batıran bir çocuğa tam yıldız vermek olurdu.
        var round = LetterTraceRound.ForBand(AgeBand.Mese, ["I"], new Random(1));
        var points = round.ActiveStroke!.Points.ToList();

        round.Begin(points[0].X, points[0].Y);
        round.MoveTo(0.95f, 0.5f);
        round.Release();

        WriteGlyph(round);

        Assert.Equal(1, round.Slips);
        Assert.Equal(1, round.Mistakes);
    }

    [Fact]
    public void Lifting_the_finger_keeps_the_progress()
    {
        var round = LetterTraceRound.ForBand(AgeBand.Fidan, ["I"], new Random(1));
        var points = round.ActiveStroke!.Points.ToList();

        round.Begin(points[0].X, points[0].Y);
        round.MoveTo(points[10].X, points[10].Y);

        var progress = round.ActiveStroke!.Progress;
        round.Release();

        Assert.Equal(progress, round.ActiveStroke!.Progress, 5);
        Assert.False(round.ActiveStroke!.IsTracing);
    }

    [Fact]
    public void The_same_seed_produces_the_same_letters()
    {
        var a = Round(AgeBand.Mese, seed: 12);
        var b = Round(AgeBand.Mese, seed: 12);

        Assert.Equal(a.Current.Character, b.Current.Character);
    }
}
