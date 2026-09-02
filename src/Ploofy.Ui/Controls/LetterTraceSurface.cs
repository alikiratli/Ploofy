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
                continue;
            }

            DrawGuide(canvas, points, band, path.IsOffPath, isDim: false);
            DrawInked(canvas, points, path.Progress * (points.Count - 1), path.Head, band);
            DrawStart(canvas, points[0], band);
            DrawHead(canvas, path, band);
        }

        DrawMarks(canvas, round.Current, band);
        _particles.Draw(canvas);
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

    private void DrawStart(SKCanvas canvas, PathPoint start, float band)
    {
        _marker.Color = PloofyPalette.Ink.WithAlpha(60);
        canvas.DrawCircle(ToScreen(start), band * 0.30f, _marker);
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
    }
}
