using System.Globalization;
using Ploofy.Engine.Games;
using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>
/// Harf Yazma'nın çizim ve takip yüzeyi.
/// </summary>
/// <remarks>
/// <para>
/// Ekranın işi tek bir şeyi anlatmak: <b>şimdi neresi çizilecek</b>. Bitmiş
/// darbeler dolu ve renkli, sıradaki darbe beyaz bir şerit, sonrakiler soluk.
/// Çocuk harfin ortaya çıkışını görüyor — oyunun bütün ödülü bu.
/// </para>
/// <para>
/// Şerit kalınlığı motorun toleransının <b>tam olarak iki katı</b>: içi kabul,
/// dışı ret. Kalınlığı süs olarak seçmek, çocuğun gördüğü çizginin üstünde
/// olduğu hâlde "çıktın" demek olurdu. Aynı kural Yolu Bul'da da geçerli.
/// </para>
/// <para>
/// Her darbenin yanında <b>sırasının rakamı</b>, ucunda da <b>yön oku</b>
/// duruyor — yazı defterlerindeki gösterimin aynısı. Motor sırayı zaten
/// dayatıyordu ama ekran onu yalnızca renkle söylüyordu: sıradaki beyaz,
/// ötekiler soluk. Rakam bunu <b>adlandırılabilir</b> yapıyor; çocuk "bir,
/// iki, üç" diyerek yazıyor ve aynı sırayı kâğıtta da kuruyor. Ok ise
/// sıranın söylemediğini söylüyor: aynı çizgi iki yönde de çizilebilir ve
/// yanlış yönde yazmayı öğrenen çocuk bunu sonradan zor bırakıyor.
/// </para>
/// <para>
/// Rakam darbenin <b>başına</b> değil, biraz içine ve dışa doğru kaçırılarak
/// konuyor. Sebebi A: iki darbesi de tepe noktasından başlıyor, yani başa
/// konan iki rakam üst üste binerdi. Yüzde on beş içeriden bakınca darbeler
/// ayrılmış oluyor.
/// </para>
/// <para>
/// Aksan işaretleri (Ç'nin kuyruğu, İ'nin noktası) hiç takip edilmiyor, baştan
/// dolu çiziliyor: bir noktanın yönü yok, dolayısıyla takip edilecek bir şeyi
/// de yok.
/// </para>
/// </remarks>
public sealed class LetterTraceSurface : SKCanvasView
{
    /// <summary>Biten işaretin ekranda kaldığı süre.</summary>
    /// <remarks>
    /// Motor son darbede hemen sıradaki işarete geçiyor. Bu pay olmadan çocuk
    /// tamamladığı harfi hiç görmüyor — ve bitirmenin ödülü tam olarak o.
    /// </remarks>
    private const float CelebrationDuration = 0.95f;

    /// <summary>Rakamın kaçış yönünde "berabere" sayılan eşik.</summary>
    /// <remarks>
    /// Birim karede ölçülüyor. Bundan küçük bir eğilim, ekranda görülebilir
    /// bir yön farkı üretmiyor — bkz. <see cref="OutwardNormal"/>.
    /// </remarks>
    private const float Tie = 0.02f;

    private static readonly SKColor GuideColor = new(0xFF, 0xFF, 0xFF);
    private static readonly SKColor SlipColor = new(0xFF, 0x8C, 0x42);

    private readonly ParticleField _particles = new() { Gravity = 420f };
    private readonly Random _rng = new();

    private readonly SKPaint _sky = new() { IsAntialias = true };
    private readonly SKPaint _stroke = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private readonly SKPaint _marker = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _number = new() { IsAntialias = true };
    private readonly SKFont _numberFont = new(SKTypeface.Default) { Embolden = true };

    private IDispatcherTimer? _ticker;
    private DateTime _lastFrame;
    private float _time;

    // Ekrandaki kare alan; dokunma dönüşümü de bunu kullanıyor.
    private float _originX;
    private float _originY;
    private float _side;

    // Biten işaret: motor sıradakine geçtikten sonra da kısa süre dursun diye.
    private Glyph? _finishedGlyph;
    private float _finishedAt = float.MinValue;

    public LetterTraceSurface()
    {
        EnableTouchEvents = true;
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
        Touch += OnTouch;
    }

    public LetterTraceRound? Round { get; private set; }

    /// <summary>Parmak bir şey yaptı: başladı, ilerledi, çıktı, darbeyi bitirdi.</summary>
    public event EventHandler<TraceEventArgs>? Traced;

    /// <summary>Bütün işaretler yazıldı.</summary>
    public event EventHandler? RoundOver;

    public void Start(LetterTraceRound round)
    {
        Round = round;
        _time = 0f;
        _finishedGlyph = null;
        _finishedAt = float.MinValue;
        _particles.Clear();

        _lastFrame = DateTime.UtcNow;
        StartTicker();
        InvalidateSurface();
    }

    public void Stop()
    {
        _ticker?.Stop();
        _ticker = null;
    }

    /// <summary>Kutlama sürerken dokunuş kapalı: sıradaki işaret henüz gösterilmedi.</summary>
    private bool IsCelebrating => _time - _finishedAt < CelebrationDuration;

    private void StartTicker()
    {
        if (_ticker is not null)
        {
            _ticker.Start();
            return;
        }

        _ticker = Dispatcher.CreateTimer();
        _ticker.Interval = TimeSpan.FromMilliseconds(16);
        _ticker.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            _time += MathF.Min((float)(now - _lastFrame).TotalSeconds, 0.05f);
            _lastFrame = now;

            _particles.Advance(1f / 60f);

            if (_finishedGlyph is not null && !IsCelebrating)
            {
                _finishedGlyph = null;

                if (Round is { IsComplete: true })
                {
                    RoundOver?.Invoke(this, EventArgs.Empty);
                }
            }

            InvalidateSurface();
        };
        _ticker.Start();
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        e.Handled = true;

        if (Round is not { } round || round.IsComplete || IsCelebrating || _side <= 0f)
        {
            return;
        }

        var x = (e.Location.X - _originX) / _side;
        var y = (e.Location.Y - _originY) / _side;

        var outcome = e.ActionType switch
        {
            SKTouchAction.Pressed => round.Begin(x, y),
            SKTouchAction.Moved => round.MoveTo(x, y),
            SKTouchAction.Released or SKTouchAction.Cancelled => Release(round),
            _ => TraceOutcome.Ignored,
        };

        // Kutlama yalnızca işaretin tamamı bitince: dört darbeli bir E'yi
        // dört kez kutlamak, bitirmenin anlamını yok ederdi.
        if (outcome == TraceOutcome.LevelComplete && round.GlyphComplete)
        {
            StartCelebration();
        }

        if (outcome != TraceOutcome.Ignored)
        {
            Traced?.Invoke(this, new TraceEventArgs(outcome));
        }

        InvalidateSurface();
    }

    private static TraceOutcome Release(LetterTraceRound round)
    {
        round.Release();
        return TraceOutcome.Ignored;
    }

    private void StartCelebration()
    {
        // Biten işaretin kopyası: motor bu noktada sıradakine geçti bile.
        _finishedGlyph = _drawnGlyph;
        _finishedAt = _time;

        var last = _drawnGlyph?.Strokes[^1];
        var burstAt = last is null ? new PathPoint(0.5f, 0.5f) : last[^1];

        _particles.Burst(ToScreen(burstAt), _side * 0.05f, PloofyPalette.Lime, _rng, count: 30);
    }

    /// <summary>Son karede çizilen işaret — kutlama kopyası bundan alınıyor.</summary>
    /// <remarks>
    /// Motor son darbede sıradaki işarete geçtiği için biten işaret artık
    /// ondan istenemiyor; ekranda görüneni saklamak tek yol.
    /// </remarks>
    private Glyph? _drawnGlyph;

    private SKPoint ToScreen(PathPoint point) =>
        new(_originX + (point.X * _side), _originY + (point.Y * _side));

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var width = e.Info.Width;
        var height = e.Info.Height;

        DrawSky(canvas, width, height);

        if (Round is not { } round)
        {
            return;
        }

        Layout(width, height);

        var band = round.Tolerance * 2f * _side;

        // Kutlama sürerken biten işaret tamamen dolu duruyor; sıradaki henüz
        // ekrana gelmiyor.
        if (_finishedGlyph is { } finished)
        {
            foreach (var stroke in finished.Strokes)
            {
                DrawGuide(canvas, stroke, band, isOffPath: false, isDim: false);
                DrawInked(canvas, stroke, stroke.Count - 1, stroke[^1], band);
            }

            DrawMarks(canvas, finished, band);
            _particles.Draw(canvas);
            return;
        }

        _drawnGlyph = round.Current;

        var center = CenterOf(round.Current);

        // Şeritler önce, rakamlar sonra: bir rakam, sonraki darbenin
        // şeridinin altında kalmamalı — harfin üstünde durmalı.
        for (var i = 0; i < round.Strokes.Count; i++)
        {
            var path = round.Strokes[i];
            var points = path.Points;

            if (i < round.StrokeIndex)
            {
                // Bitmiş: dolu kalıyor, harf gözümüzün önünde birikiyor.
                DrawGuide(canvas, points, band, isOffPath: false, isDim: false);
                DrawInked(canvas, points, points.Count - 1, points[^1], band);
                continue;
            }

            if (i > round.StrokeIndex)
            {
                // Sıradaki değil: soluk dursun ki hangisinin çizileceği
                // tereddütsüz belli olsun.
                DrawGuide(canvas, points, band, isOffPath: false, isDim: true);
                DrawArrow(canvas, points, band, isDim: true);
                continue;
            }

            DrawGuide(canvas, points, band, path.IsOffPath, isDim: false);
            DrawInked(canvas, points, path.Progress * (points.Count - 1), path.Head, band);
            DrawArrow(canvas, points, band, isDim: false);
            DrawHead(canvas, path, band);
        }

        for (var i = 0; i < round.Strokes.Count; i++)
        {
            DrawStrokeNumber(
                canvas,
                round.Strokes[i].Points,
                i + 1,
                band,
                center,
                isDone: i < round.StrokeIndex,
                isActive: i == round.StrokeIndex);
        }

        DrawMarks(canvas, round.Current, band);
        _particles.Draw(canvas);
    }

    /// <summary>
    /// İşaretin ağırlık merkezi, birim karede.
    /// </summary>
    /// <remarks>
    /// Rakamların hangi yöne kaçırılacağı buradan çıkıyor: merkezden uzağa,
    /// yani harfin dışına. Sabit bir yön (hep sola, hep yukarı) bazı
    /// harflerde rakamı gövdenin üstüne düşürüyordu.
    /// </remarks>
    private static (float X, float Y) CenterOf(Glyph glyph)
    {
        float sumX = 0, sumY = 0;
        var count = 0;

        foreach (var stroke in glyph.Strokes)
        {
            foreach (var point in stroke)
            {
                sumX += point.X;
                sumY += point.Y;
                count++;
            }
        }

        return count == 0 ? (0.5f, 0.5f) : (sumX / count, sumY / count);
    }

    /// <summary>
    /// Birim kareyi ekrana oturtur.
    /// </summary>
    /// <remarks>
    /// Üstte bilgi şeridi için pay bırakılıyor: kare tam ortalanınca harfin
    /// üst ucu şeridin altında kalıyor ve çocuk oraya parmak koyamıyor.
    /// </remarks>
    private void Layout(float width, float height)
    {
        var top = height * 0.11f;
        _side = MathF.Min(width * 0.94f, (height - top) * 0.94f);
        _originX = (width - _side) / 2f;
        _originY = top + ((height - top - _side) / 2f);
    }

    private void DrawSky(SKCanvas canvas, float width, float height)
    {
        _sky.Shader?.Dispose();
        _sky.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, height),
            [PloofyPalette.SkyTop, PloofyPalette.SkyMiddle, PloofyPalette.SkyBottom],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, width, height, _sky);
    }

    private SKPath BuildPath(IReadOnlyList<PathPoint> points, float upTo, PathPoint? end = null)
    {
        var path = new SKPath();
        var last = Math.Min((int)upTo, points.Count - 1);

        path.MoveTo(ToScreen(points[0]));
        for (var i = 1; i <= last; i++)
        {
            path.LineTo(ToScreen(points[i]));
        }

        if (end is { } tip)
        {
            path.LineTo(ToScreen(tip));
        }

        return path;
    }

    /// <summary>Çizilecek şerit: harfin boş hâli.</summary>
    private void DrawGuide(
        SKCanvas canvas, IReadOnlyList<PathPoint> points, float band, bool isOffPath, bool isDim)
    {
        using var path = BuildPath(points, points.Count - 1);

        _stroke.Color = isOffPath
            // Çizgiden çıkınca kenar turuncuya dönüyor: "buraya geri gel".
            ? SlipColor.WithAlpha(190)
            : PloofyPalette.Ink.WithAlpha((byte)(isDim ? 18 : 38));
        _stroke.StrokeWidth = band + (band * 0.22f);
        canvas.DrawPath(path, _stroke);

        _stroke.Color = GuideColor.WithAlpha((byte)(isDim ? 95 : 225));
        _stroke.StrokeWidth = band;
        canvas.DrawPath(path, _stroke);
    }

    /// <summary>Çizilmiş kısım — şeridin üstüne boyanıyor.</summary>
    private void DrawInked(
        SKCanvas canvas,
        IReadOnlyList<PathPoint> points,
        float upTo,
        PathPoint head,
        float band)
    {
        if (upTo <= 0f)
        {
            return;
        }

        using var path = BuildPath(points, upTo, head);

        var lime = PloofyPalette.Lime;

        _stroke.Color = lime.Body;
        _stroke.StrokeWidth = band * 0.74f;
        canvas.DrawPath(path, _stroke);

        _stroke.Color = lime.Light.WithAlpha(150);
        _stroke.StrokeWidth = band * 0.34f;
        canvas.DrawPath(path, _stroke);
    }

    /// <summary>Aksan işaretleri: baştan dolu, hiç takip edilmiyor.</summary>
    private void DrawMarks(SKCanvas canvas, Glyph glyph, float band)
    {
        foreach (var mark in glyph.Marks)
        {
            using var path = BuildPath(mark, mark.Count - 1);

            _stroke.Color = PloofyPalette.Ink.WithAlpha(150);
            _stroke.StrokeWidth = band * 0.46f;
            canvas.DrawPath(path, _stroke);
        }
    }

    /// <summary>
    /// Darbenin sıra rakamı.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Yazı defterlerindeki gösterim. Darbenin başında değil yüzde on beş
    /// içinde duruyor ve gövdenin dışına kaçırılıyor: A harfinin iki darbesi
    /// de tepeden başlıyor, başa konan iki rakam üst üste binerdi.
    /// </para>
    /// <para>
    /// Bitmiş darbenin rakamı da duruyor ama sönük: çocuk kaçıncı darbede
    /// olduğunu ancak öncekileri de görerek sayabiliyor.
    /// </para>
    /// </remarks>
    private void DrawStrokeNumber(
        SKCanvas canvas,
        IReadOnlyList<PathPoint> points,
        int number,
        float band,
        (float X, float Y) center,
        bool isDone,
        bool isActive)
    {
        if (points.Count < 2)
        {
            return;
        }

        var head = Math.Min(points.Count - 1, (int)(points.Count * 0.15f));
        var anchor = points[head];

        var (nx, ny) = OutwardNormal(points, head, anchor, center);

        // Yarıçap pikselde (daire onunla çiziliyor), kaçış payı ve kenar payı
        // birim karede (rakamın yeri orada hesaplanıyor). İkisini karıştırmak
        // rakamı her harfte ekranın kenarına yapıştırıyordu.
        var radius = band * 0.34f;
        var offset = band * 0.78f / _side;
        var margin = radius / _side;

        var badge = new PathPoint(
            Math.Clamp(anchor.X + (nx * offset), margin, 1f - margin),
            Math.Clamp(anchor.Y + (ny * offset), margin, 1f - margin));

        var screen = ToScreen(badge);
        var hue = isDone ? PloofyPalette.Lime : PloofyPalette.Grape;
        var alpha = isActive ? (byte)255 : isDone ? (byte)120 : (byte)150;

        _marker.Color = SKColors.White.WithAlpha(alpha);
        canvas.DrawCircle(screen, radius, _marker);

        _marker.Color = hue.Body.WithAlpha(alpha);
        canvas.DrawCircle(screen, radius * 0.82f, _marker);

        var text = number.ToString(CultureInfo.InvariantCulture);
        _numberFont.Size = radius * 1.05f;

        var metrics = _numberFont.Metrics;
        var baseline = screen.Y - ((metrics.Ascent + metrics.Descent) / 2f);

        _number.Color = SKColors.White.WithAlpha(alpha);
        canvas.DrawText(text, screen.X, baseline, SKTextAlign.Center, _numberFont, _number);
    }

    /// <summary>
    /// Rakamın kaçacağı yön: darbeye <b>dik</b>, gövdenin dışına doğru.
    /// </summary>
    /// <remarks>
    /// <para>
    /// İlk deneme yönü merkezden dışa doğru alıyordu ve A, B, E ile 4'te
    /// rakamlar üst üste biniyordu: A'nın iki darbesi de tepeden başlıyor,
    /// merkez de tam altlarında, yani ikisi de neredeyse dümdüz yukarı
    /// kaçıyordu. Dik yön onları darbelerin <b>iki yanına</b> ayırıyor —
    /// yazı defterlerindeki yerleşim de bu.
    /// </para>
    /// <para>
    /// İki dikten hangisi: gövdeden uzaklaşan. Bu ölçü bazı harflerde
    /// beraberliğe düşüyor ve beraberlik iki kademede bozuluyor:
    /// </para>
    /// <para>
    /// Önce yatayda. X'in bir köşegeninin diki tam olarak öteki köşegen
    /// olduğu için her iki dik de merkeze eşit uzaklıkta; ölçü karar
    /// veremeyince iki rakam da ortaya kaçıp üst üste biniyordu. Darbenin
    /// hangi yanda başladığına bakmak onları X'in iki dış yanına ayırıyor.
    /// </para>
    /// <para>
    /// Yatay da karar vermezse (E'nin orta kolu: diki tam dikey, yani
    /// yatayda hiçbir tarafa eğilimi yok) yukarısı seçiliyor — rakamın
    /// altında kalan harf onu okunmaz yapıyor.
    /// </para>
    /// </remarks>
    private static (float X, float Y) OutwardNormal(
        IReadOnlyList<PathPoint> points, int at, PathPoint anchor, (float X, float Y) center)
    {
        // Yön kısa bir komşuluktan alınıyor; iki bitişik nokta örneklenmiş
        // bir yayda neredeyse üst üste ve aradaki yön gürültüye açık.
        var ahead = Math.Min(points.Count - 1, at + Math.Max(1, points.Count / 8));

        var dx = points[ahead].X - anchor.X;
        var dy = points[ahead].Y - anchor.Y;

        if (MathF.Sqrt((dx * dx) + (dy * dy)) < 1e-4f)
        {
            // Komşuluk çöktü: darbenin tamamının yönüne düş.
            dx = points[^1].X - points[0].X;
            dy = points[^1].Y - points[0].Y;
        }

        var length = MathF.Sqrt((dx * dx) + (dy * dy));
        if (length < 1e-4f)
        {
            return (0f, -1f);
        }

        var nx = -dy / length;
        var ny = dx / length;

        var awayX = anchor.X - center.X;
        var awayY = anchor.Y - center.Y;
        var dot = (nx * awayX) + (ny * awayY);

        bool flip;
        if (MathF.Abs(dot) >= Tie)
        {
            flip = dot < 0f;
        }
        else if (MathF.Abs(nx) >= Tie && MathF.Abs(awayX) >= Tie)
        {
            // Beraberlik, birinci kademe: darbe gövdenin hangi yanında
            // başlıyorsa rakam o yana.
            flip = (nx < 0f) != (awayX < 0f);
        }
        else
        {
            // Beraberlik, ikinci kademe: yukarısı.
            flip = ny > 0f;
        }

        if (flip)
        {
            (nx, ny) = (-nx, -ny);
        }

        return (nx, ny);
    }

    /// <summary>
    /// Darbenin ucundaki yön oku.
    /// </summary>
    /// <remarks>
    /// Sıra tek başına yetmiyor: aynı çizgi iki yönde de çizilebilir ve
    /// motorun kabul ettiği tek yön var. Ok o yönü söylüyor — yoksa çocuk
    /// doğru çizgiye doğru sırayla dokunup neden ilerlemediğini anlamıyor.
    /// </remarks>
    private void DrawArrow(
        SKCanvas canvas, IReadOnlyList<PathPoint> points, float band, bool isDim)
    {
        if (points.Count < 2)
        {
            return;
        }

        var tip = points[^1];

        // Yön son iki noktadan değil, son beşte birden alınıyor: örneklenmiş
        // bir yayın son iki noktası neredeyse üst üste ve aradaki yön
        // gürültüye açık.
        var from = points[Math.Max(0, points.Count - 1 - (points.Count / 5))];

        var dx = tip.X - from.X;
        var dy = tip.Y - from.Y;
        var length = MathF.Sqrt((dx * dx) + (dy * dy));

        if (length < 1e-4f)
        {
            return;
        }

        dx /= length;
        dy /= length;

        var size = band * 0.42f;
        var head = ToScreen(tip);

        // Uç şeridin tam sonuna değil biraz gerisine oturuyor: uçtaki bir ok,
        // yuvarlak biten şeridin dışına taşıyor.
        head = new SKPoint(head.X - (dx * size * 0.25f), head.Y - (dy * size * 0.25f));

        var backX = head.X - (dx * size);
        var backY = head.Y - (dy * size);
        var wing = size * 0.55f;

        using var arrow = new SKPath();
        arrow.MoveTo(head);
        arrow.LineTo(backX - (dy * wing), backY + (dx * wing));
        arrow.LineTo(backX + (dy * wing), backY - (dx * wing));
        arrow.Close();

        _marker.Color = PloofyPalette.Grape.Body.WithAlpha(isDim ? (byte)70 : (byte)210);
        canvas.DrawPath(arrow, _marker);
    }

    /// <summary>
    /// İlerlemenin ucu.
    /// </summary>
    /// <remarks>
    /// Parmak kalkınca ilerleme korunuyor ama nereden devam edileceği ancak
    /// bu işaretle belli oluyor — motor da yalnızca buraya konan parmağı
    /// kabul ediyor. Parmak çizgideyken işaret sönüyor: altında zaten parmak var.
    /// </remarks>
    private void DrawHead(SKCanvas canvas, TracePath path, float band)
    {
        var center = ToScreen(path.Head);
        var hue = PloofyPalette.Cherry;

        if (path.IsTracing)
        {
            _marker.Color = hue.Body.WithAlpha(110);
            canvas.DrawCircle(center, band * 0.26f, _marker);
            return;
        }

        // Beklerken nabız gibi atıyor: hareketsiz bir nokta ekranda kayboluyor.
        var pulse = 1f + (0.22f * MathF.Sin(_time * 5.2f));

        _marker.Color = hue.Light.WithAlpha(80);
        canvas.DrawCircle(center, band * 0.52f * pulse, _marker);

        _marker.Color = hue.Body;
        canvas.DrawCircle(center, band * 0.28f, _marker);

        _marker.Color = SKColors.White.WithAlpha(200);
        canvas.DrawCircle(center, band * 0.12f, _marker);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null)
        {
            return;
        }

        Stop();
        _particles.Dispose();
        _sky.Shader?.Dispose();
        _sky.Dispose();
        _stroke.Dispose();
        _marker.Dispose();
        _number.Dispose();
        _numberFont.Dispose();
    }
}
