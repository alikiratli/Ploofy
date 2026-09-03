using Ploofy.Engine.Games;
using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>Bir boyama dokunuşunun sonucu.</summary>
public sealed class PaintEventArgs(PaintOutcome outcome) : EventArgs
{
    public PaintOutcome Outcome { get; } = outcome;
}

/// <summary>
/// Boyama'nın çizim yüzeyi.
/// </summary>
/// <remarks>
/// <para>
/// Alanlar çokgen ve boyalar düz renk: gölge, degrade ve doku yok. Sebebi
/// çocuk: boyanmış alan ile boyanmamış alan arasındaki farkın tereddütsüz
/// görünmesi gerekiyor, süs o farkı bulanıklaştırıyor.
/// </para>
/// <para>
/// Boyanmamış alan beyaz ve koyu konturlu — boyama kitabındaki gibi. Kontur
/// her zaman çiziliyor, boyandıktan sonra da: alanların sınırı kaybolursa
/// resim tek bir renk lekesine dönüyor.
/// </para>
/// <para>
/// Motorun resmi <b>birim kare</b> içinde yaşıyor; burası o kareyi ekranın
/// ortasına oturtuyor. Ekranın tamamına yaymak evi de balığı da yatayda
/// ezerdi.
/// </para>
/// </remarks>
public sealed class ColoringSurface : SKCanvasView
{
    /// <summary>Biten resmin ekranda kaldığı süre.</summary>
    /// <remarks>
    /// Motor son alanda hemen sıradaki resme geçiyor. Bu pay olmadan çocuk
    /// bitirdiği resmi hiç görmüyor — ve bu oyunun tek ödülü tam olarak o.
    /// </remarks>
    private const float CelebrationDuration = 1.6f;

    private readonly ParticleField _particles = new() { Gravity = 380f };
    private readonly Random _rng = new();

    private readonly SKPaint _backdrop = new() { IsAntialias = true };
    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _outline = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private IDispatcherTimer? _ticker;
    private DateTime _lastFrame;
    private float _time;

    private float _originX;
    private float _originY;
    private float _side;

    // Biten resim: motor sıradakine geçtikten sonra da kısa süre dursun diye.
    private ColoringPicture? _finishedPicture;
    private Dictionary<string, int>? _finishedFills;
    private float _finishedAt = float.MinValue;

    public ColoringSurface()
    {
        EnableTouchEvents = true;
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
        Touch += OnTouch;
    }

    public ColoringRound? Round { get; private set; }

    /// <summary>Bir alan boyandı ya da resim bitti.</summary>
    public event EventHandler<PaintEventArgs>? Painted;

    /// <summary>Bütün resimler tamamlandı.</summary>
    public event EventHandler? RoundOver;

    public void Start(ColoringRound round)
    {
        Round = round;
        _time = 0f;
        _finishedPicture = null;
        _finishedFills = null;
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

            if (_finishedPicture is not null && !IsCelebrating)
            {
                _finishedPicture = null;
                _finishedFills = null;

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

        // Yalnızca parmağın konduğu an. Sürüklemeyi de kabul etmek, ekranda
        // gezinen bir parmağın bütün resmi tek renge boyaması demekti.
        if (e.ActionType != SKTouchAction.Pressed)
        {
            return;
        }

        if (Round is not { } round || round.IsComplete || IsCelebrating || _side <= 0f)
        {
            return;
        }

        var picture = round.Current;
        var fills = round.Fills.ToDictionary(p => p.Key, p => p.Value, StringComparer.Ordinal);

        var x = (e.Location.X - _originX) / _side;
        var y = (e.Location.Y - _originY) / _side;

        var outcome = round.Paint(x, y);

        if (outcome == PaintOutcome.PictureComplete)
        {
            StartCelebration(picture, fills, round.SelectedColor, x, y);
        }

        if (outcome != PaintOutcome.Missed)
        {
            Painted?.Invoke(this, new PaintEventArgs(outcome));
        }

        InvalidateSurface();
    }

    /// <summary>
    /// Biten resmi ekranda tutar.
    /// </summary>
    /// <remarks>
    /// Motor bu noktada sıradaki resme geçip dolguları temizledi, o yüzden
    /// hem resim hem dolgular kopyalanıyor. Son dokunulan alanın rengi de
    /// elle ekleniyor: kopya, o dokunuştan <b>önce</b> alındı.
    /// </remarks>
    private void StartCelebration(
        ColoringPicture picture,
        Dictionary<string, int> fills,
        int lastColor,
        float x,
        float y)
    {
        if (picture.HitTest(x, y) is { } last)
        {
            fills[last.Id] = lastColor;
        }

        _finishedPicture = picture;
        _finishedFills = fills;
        _finishedAt = _time;

        var cx = picture.Regions.SelectMany(r => r.Outline).Average(p => p.X);
        var cy = picture.Regions.SelectMany(r => r.Outline).Average(p => p.Y);

        _particles.Burst(
            ToScreen(new ColorPoint(cx, cy)),
            _side * 0.12f,
            PloofyPalette.All[lastColor % PloofyPalette.All.Count],
            _rng,
            count: 44);
    }

    private SKPoint ToScreen(ColorPoint point) =>
        new(_originX + (point.X * _side), _originY + (point.Y * _side));

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var width = e.Info.Width;
        var height = e.Info.Height;

        DrawBackdrop(canvas, width, height);

        if (Round is not { } round)
        {
            return;
        }

        Layout(width, height);

        if (_finishedPicture is { } finished && _finishedFills is { } fills)
        {
            DrawPicture(canvas, finished, fills);
            _particles.Draw(canvas);
            return;
        }

        DrawPicture(canvas, round.Current, round.Fills);
        _particles.Draw(canvas);
    }

    /// <summary>
    /// Birim kareyi ekrana oturtur.
    /// </summary>
    /// <remarks>
    /// Üstte bilgi şeridi, altta palet için pay bırakılıyor: kare tam
    /// ortalanınca resmin alt ucu paletin altında kalıyor ve çocuk oraya
    /// parmak koyamıyor.
    /// </remarks>
    private void Layout(float width, float height)
    {
        var top = height * 0.11f;
        var bottom = height * 0.06f;

        _side = MathF.Min(width * 0.90f, (height - top - bottom) * 0.96f);
        _originX = (width - _side) / 2f;
        _originY = top + ((height - top - bottom - _side) / 2f);
    }

    private void DrawBackdrop(SKCanvas canvas, float width, float height)
    {
        _backdrop.Shader?.Dispose();
        _backdrop.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, height),
            [PloofyPalette.SkyTop, PloofyPalette.SkyMiddle, PloofyPalette.SkyBottom],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, width, height, _backdrop);
    }

    private void DrawPicture(
        SKCanvas canvas, ColoringPicture picture, IReadOnlyDictionary<string, int> fills)
    {
        // Kağıt: resmin altındaki beyaz zemin. Boyama kitabındaki sayfa.
        _fill.Color = SKColors.White;
        canvas.DrawRoundRect(
            _originX, _originY, _side, _side, _side * 0.04f, _side * 0.04f, _fill);

        _outline.StrokeWidth = MathF.Max(2f, _side * 0.008f);

        foreach (var region in picture.Regions)
        {
            using var path = BuildPath(region);

            _fill.Color = fills.TryGetValue(region.Id, out var color)
                ? PloofyPalette.All[color % PloofyPalette.All.Count].Body
                : SKColors.White;
            canvas.DrawPath(path, _fill);

            _outline.Color = PloofyPalette.Ink.WithAlpha(210);
            canvas.DrawPath(path, _outline);
        }
    }

    private SKPath BuildPath(ColoringRegion region)
    {
        var path = new SKPath();

        path.MoveTo(ToScreen(region.Outline[0]));
        for (var i = 1; i < region.Outline.Count; i++)
        {
            path.LineTo(ToScreen(region.Outline[i]));
        }

        path.Close();
        return path;
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
        _backdrop.Shader?.Dispose();
        _backdrop.Dispose();
        _fill.Dispose();
        _outline.Dispose();
    }
}
