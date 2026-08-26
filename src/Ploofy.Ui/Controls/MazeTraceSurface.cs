using Ploofy.Engine.Games;
using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>Parmağın yaptığı şeyin ekrandaki karşılığı.</summary>
public sealed class TraceEventArgs(TraceOutcome outcome) : EventArgs
{
    public TraceOutcome Outcome { get; } = outcome;
}

/// <summary>
/// Yolu Bul'un çizim ve takip yüzeyi.
/// </summary>
/// <remarks>
/// <para>
/// Motorun yolu <b>birim kare</b> içinde yaşıyor; burası o kareyi ekranın
/// ortasına oturtuyor. Ekranın tamamına yaymak, yoldan çıkmayı yatayda ve
/// dikeyde farklı mesafeler hâline getirirdi.
/// </para>
/// <para>
/// Çizilen yol kalınlığı motorun toleransının <b>tam olarak iki katı</b>:
/// şeridin içi kabul, dışı ret. Kalınlığı süs olarak seçmek, çocuğun
/// gördüğü yolun üstünde olduğu hâlde "çıktın" demek olurdu.
/// </para>
/// </remarks>
public sealed class MazeTraceSurface : SKCanvasView
{
    /// <summary>Biten yolun ekranda kaldığı süre.</summary>
    /// <remarks>
    /// Motor son adımda hemen yeni yolu veriyor. Bu pay olmadan çocuk
    /// bitirdiği yolu hiç görmüyor — ve bitirmenin ödülü tam olarak o.
    /// </remarks>
    private const float CelebrationDuration = 0.85f;

    private static readonly SKColor RoadColor = new(0xFF, 0xFF, 0xFF);
    private static readonly SKColor SlipColor = new(0xFF, 0x8C, 0x42);

    private readonly ShapePainter _painter = new();
    private readonly ParticleField _particles = new() { Gravity = 500f };
    private readonly Random _rng = new();

    private readonly SKPaint _sky = new() { IsAntialias = true };
    private readonly SKPaint _road = new()
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

    // Biten yol: motor yeni yola geçtikten sonra da kısa süre çizilsin diye.
    private List<PathPoint>? _finishedPath;
    private float _finishedAt = float.MinValue;

    /// <summary>Son karede çizilen yol — kutlama kopyası bundan alınıyor.</summary>
    /// <remarks>
    /// Motor son adımda yeni yolu kurduğu için biten yol artık ondan
    /// istenemiyor; ekranda görüneni saklamak tek yol.
    /// </remarks>
    private List<PathPoint> _lastDrawnPath = [];

    public MazeTraceSurface()
    {
        EnableTouchEvents = true;
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
        Touch += OnTouch;
    }

    public MazeTraceRound? Round { get; private set; }

    /// <summary>Parmak bir şey yaptı: başladı, ilerledi, çıktı, bitirdi.</summary>
    public event EventHandler<TraceEventArgs>? Traced;

    /// <summary>Bütün yollar tamamlandı.</summary>
    public event EventHandler? RoundOver;

    public void Start(MazeTraceRound round)
    {
        Round = round;
        _time = 0f;
        _finishedPath = null;
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

    /// <summary>Kutlama sürerken dokunuş kapalı: yeni yol henüz gösterilmedi.</summary>
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

            if (_finishedPath is not null && !IsCelebrating)
            {
                _finishedPath = null;

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

        if (outcome == TraceOutcome.LevelComplete)
        {
            StartCelebration(round);
        }

        if (outcome != TraceOutcome.Ignored)
        {
            Traced?.Invoke(this, new TraceEventArgs(outcome));
        }

        InvalidateSurface();
    }

    private static TraceOutcome Release(MazeTraceRound round)
    {
        round.Release();
        return TraceOutcome.Ignored;
    }

    private void StartCelebration(MazeTraceRound round)
    {
        // Biten yolun kopyası alınıyor: motor bu noktada yeni yolu kurdu bile.
        _finishedPath = [.. _lastDrawnPath];
        _finishedAt = _time;

        var goal = _lastDrawnPath.Count > 0 ? _lastDrawnPath[^1] : round.Goal;
        _particles.Burst(
            ToScreen(goal),
            _side * 0.06f,
            PloofyPalette.Lime,
            _rng,
            count: 26);
    }

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

        // Kutlama sürerken biten yol tam dolu olarak duruyor; yeni yol henüz
        // ekrana gelmiyor.
        if (_finishedPath is { Count: > 1 } finished)
        {
            var roadWidth = round.Tolerance * 2f * _side;
            DrawRoad(canvas, finished, roadWidth, isOffPath: false);
            DrawTravelled(canvas, finished, finished.Count - 1, finished[^1], roadWidth);
            DrawGoal(canvas, finished[^1], roadWidth, isReached: true);
            _particles.Draw(canvas);
            return;
        }

        _lastDrawnPath = [.. round.Points];

        var road = round.Tolerance * 2f * _side;
        DrawRoad(canvas, _lastDrawnPath, road, round.IsOffPath);

        var headIndex = round.Progress * (_lastDrawnPath.Count - 1);
        DrawTravelled(canvas, _lastDrawnPath, headIndex, round.Head, road);

        DrawStart(canvas, round.Start, road);
        DrawGoal(canvas, round.Goal, road, isReached: false);
        DrawHead(canvas, round, road);

        _particles.Draw(canvas);
    }

    /// <summary>
    /// Birim kareyi ekrana oturtur.
    /// </summary>
    /// <remarks>
    /// Üstte bilgi şeridi için pay bırakılıyor: kare tam ortalanınca yolun
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

    private void DrawRoad(
        SKCanvas canvas, IReadOnlyList<PathPoint> points, float roadWidth, bool isOffPath)
    {
        using var path = BuildPath(points, points.Count - 1);

        // Dış kılıf: yolun kenarını gökyüzünden ayırıyor.
        _road.PathEffect?.Dispose();
        _road.PathEffect = null;
        _road.Color = isOffPath
            // Yoldan çıkınca kenar turuncuya dönüyor: "buraya geri gel".
            ? SlipColor.WithAlpha(190)
            : PloofyPalette.Ink.WithAlpha(38);
        _road.StrokeWidth = roadWidth + (roadWidth * 0.22f);
        canvas.DrawPath(path, _road);

        _road.Color = RoadColor.WithAlpha(225);
        _road.StrokeWidth = roadWidth;
        canvas.DrawPath(path, _road);

        // Ortadaki kesik çizgi yolu bir yol yapıyor ve yönü okutuyor.
        _road.Color = PloofyPalette.Ink.WithAlpha(45);
        _road.StrokeWidth = MathF.Max(2f, roadWidth * 0.07f);
        _road.PathEffect?.Dispose();
        _road.PathEffect = SKPathEffect.CreateDash([roadWidth * 0.34f, roadWidth * 0.30f], 0);
        canvas.DrawPath(path, _road);
        _road.PathEffect?.Dispose();
        _road.PathEffect = null;
    }

    /// <summary>Geçilen kısım — yolun üstüne boyanıyor.</summary>
    private void DrawTravelled(
        SKCanvas canvas,
        IReadOnlyList<PathPoint> points,
        float upTo,
        PathPoint head,
        float roadWidth)
    {
        if (upTo <= 0f)
        {
            return;
        }

        using var path = BuildPath(points, upTo, head);

        _road.PathEffect?.Dispose();
        _road.PathEffect = null;

        var lime = PloofyPalette.Lime;
        _road.Color = lime.Body;
        _road.StrokeWidth = roadWidth * 0.74f;
        canvas.DrawPath(path, _road);

        _road.Color = lime.Light.WithAlpha(150);
        _road.StrokeWidth = roadWidth * 0.34f;
        canvas.DrawPath(path, _road);
    }

    private void DrawStart(SKCanvas canvas, PathPoint start, float roadWidth)
    {
        _marker.Color = PloofyPalette.Ink.WithAlpha(60);
        canvas.DrawCircle(ToScreen(start), roadWidth * 0.30f, _marker);
    }

    private void DrawGoal(SKCanvas canvas, PathPoint goal, float roadWidth, bool isReached)
    {
        // Hedef bir yıldız: uygulamanın ödül dili zaten yıldız, çocuk nereye
        // gitmesi gerektiğini açıklamaya gerek kalmadan biliyor.
        var hue = isReached ? PloofyPalette.Lime : PloofyPalette.Sunny;
        var pulse = isReached ? 1.2f : 1f + (0.06f * MathF.Sin(_time * 3.4f));

        _painter.Draw(canvas, ToScreen(goal), roadWidth * 0.58f, ShapeKind.Star, hue, pulse);
    }

    /// <summary>
    /// İlerlemenin ucu.
    /// </summary>
    /// <remarks>
    /// Parmak kalkınca ilerleme korunuyor ama nereden devam edileceği ancak
    /// bu işaretle belli oluyor — motor da yalnızca buraya konan parmağı
    /// kabul ediyor. Parmak yoldayken işaret sönüyor: altında zaten parmak var.
    /// </remarks>
    private void DrawHead(SKCanvas canvas, MazeTraceRound round, float roadWidth)
    {
        if (round.Progress >= 1f)
        {
            return;
        }

        var center = ToScreen(round.Head);
        var hue = PloofyPalette.Cherry;

        if (round.IsTracing)
        {
            _marker.Color = hue.Body.WithAlpha(110);
            canvas.DrawCircle(center, roadWidth * 0.26f, _marker);
            return;
        }

        // Beklerken nabız gibi atıyor: hareketsiz bir nokta ekranda kayboluyor.
        var pulse = 1f + (0.22f * MathF.Sin(_time * 5.2f));

        _marker.Color = hue.Light.WithAlpha(80);
        canvas.DrawCircle(center, roadWidth * 0.52f * pulse, _marker);

        _marker.Color = hue.Body;
        canvas.DrawCircle(center, roadWidth * 0.28f, _marker);

        _marker.Color = SKColors.White.WithAlpha(200);
        canvas.DrawCircle(center, roadWidth * 0.12f, _marker);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null)
        {
            return;
        }

        Stop();
        _painter.Dispose();
        _particles.Dispose();
        _sky.Shader?.Dispose();
        _road.PathEffect?.Dispose();
        _sky.Dispose();
        _road.Dispose();
        _marker.Dispose();
    }
}
