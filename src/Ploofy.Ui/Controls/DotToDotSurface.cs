using System.Globalization;
using Ploofy.Engine.Games;
using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>Bir dokunuşun ekrandaki karşılığı.</summary>
public sealed class DotTapEventArgs(DotTapResult result) : EventArgs
{
    public DotTapResult Result { get; } = result;
}

/// <summary>
/// Noktaları Birleştir'in çizim yüzeyi.
/// </summary>
/// <remarks>
/// <para>
/// Motorun resmi <b>birim kare</b> içinde yaşıyor; burası o kareyi ekranın
/// ortasına oturtuyor. Ekranın tamamına yaymak resmi yatayda ezerdi ve
/// dokunma toleransı iki eksende farklı büyüklükte olurdu.
/// </para>
/// <para>
/// Noktanın çizilen yarıçapı, motorun toleransının yarısı kadar: çocuğun
/// gördüğü daireye bastığında her zaman kabul ediliyor, ama biraz ıskalaması
/// da affediliyor. Tersi — dairenin toleranstan büyük çizilmesi — "tam
/// üstüne bastım ama saymadı" demek olurdu.
/// </para>
/// <para>
/// Bitmiş resim kısa süre ekranda kalıyor. Motor son noktada hemen sıradaki
/// resme geçiyor; bu pay olmadan çocuk çizdiği hayvanı hiç görmüyor — ve
/// oyunun ödülü tam olarak o.
/// </para>
/// </remarks>
public sealed class DotToDotSurface : SKCanvasView
{
    /// <summary>Biten resmin ekranda kaldığı süre.</summary>
    private const float CelebrationDuration = 1.5f;

    /// <summary>Yanlış noktanın kırmızı yanıp sönme süresi.</summary>
    private const float WrongFlashDuration = 0.45f;

    private readonly ParticleField _particles = new() { Gravity = 420f };
    private readonly Random _rng = new();

    private readonly SKPaint _sky = new() { IsAntialias = true };

    private readonly SKPaint _line = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeCap = SKStrokeCap.Round,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private readonly SKPaint _dot = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _number = new() { IsAntialias = true };
    private readonly SKFont _numberFont = new(SKTypeface.Default);

    private IDispatcherTimer? _ticker;
    private DateTime _lastFrame;
    private float _time;

    // Ekrandaki kare alan; dokunma dönüşümü de bunu kullanıyor.
    private float _originX;
    private float _originY;
    private float _side;

    // Biten resim: motor sıradakine geçtikten sonra da kısa süre çizilsin diye.
    private DotPicture? _finishedPicture;
    private float _finishedAt = float.MinValue;

    // Yanlış dokunulan nokta, kısa süre kırmızı.
    private int _wrongDot = -1;
    private float _wrongAt = float.MinValue;

    public DotToDotSurface()
    {
        EnableTouchEvents = true;
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
        Touch += OnTouch;
    }

    public DotToDotRound? Round { get; private set; }

    /// <summary>Bir noktaya dokunuldu: bağlandı, yanlıştı ya da resim bitti.</summary>
    public event EventHandler<DotTapEventArgs>? Tapped;

    /// <summary>Bütün resimler tamamlandı.</summary>
    public event EventHandler? RoundOver;

    public void Start(DotToDotRound round)
    {
        Round = round;
        _time = 0f;
        _finishedPicture = null;
        _finishedAt = float.MinValue;
        _wrongDot = -1;
        _wrongAt = float.MinValue;
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

    /// <summary>Kutlama sürerken dokunuş kapalı: sıradaki resim henüz gösterilmedi.</summary>
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

        // Yalnızca parmağın <b>konduğu</b> an sayılıyor. Sürüklemeyi de kabul
        // etmek, ekranda gezinen bir parmağın resmi kendiliğinden çizmesi
        // demekti — oyunun tamamı o zaman kayboluyor.
        if (e.ActionType != SKTouchAction.Pressed)
        {
            return;
        }

        if (Round is not { } round || round.IsComplete || IsCelebrating || _side <= 0f)
        {
            return;
        }

        var picture = round.Current;
        var x = (e.Location.X - _originX) / _side;
        var y = (e.Location.Y - _originY) / _side;

        var result = round.Tap(x, y);

        switch (result)
        {
            case DotTapResult.Connected:
                Spark(picture.Dots[round.NextDot - 1], PloofyPalette.Lime, count: 10);
                break;

            case DotTapResult.PictureComplete:
                StartCelebration(picture);
                break;

            case DotTapResult.Wrong:
                // Yanlış nokta kısa süre kırmızı yanıyor. Sesle birlikte bu,
                // "orası değil" demenin okuma gerektirmeyen tek yolu.
                _wrongDot = NearestDrawnDot(picture, x, y);
                _wrongAt = _time;
                break;
        }

        if (result != DotTapResult.Ignored)
        {
            Tapped?.Invoke(this, new DotTapEventArgs(result));
        }

        InvalidateSurface();
    }

    /// <summary>
    /// Yanıp sönecek noktayı bulur.
    /// </summary>
    /// <remarks>
    /// Motor "yanlış" diyor ama hangisine dokunulduğunu söylemiyor — söylemesi
    /// de gerekmiyor, oradaki iş kural. Ekran için gereken hangisinin
    /// yanacağı, o da aynı en yakınlık ölçüsüyle burada bulunuyor.
    /// </remarks>
    private int NearestDrawnDot(DotPicture picture, float x, float y)
    {
        var best = -1;
        var bestDistance = float.MaxValue;

        for (var i = 0; i < picture.Count; i++)
        {
            var dot = picture.Dots[i];
            var dx = dot.X - x;
            var dy = dot.Y - y;
            var distance = (dx * dx) + (dy * dy);

            if (distance < bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }

    private void StartCelebration(DotPicture picture)
    {
        // Biten resmin kopyası: motor bu noktada sıradakine geçti bile.
        _finishedPicture = picture;
        _finishedAt = _time;
        _wrongDot = -1;

        // Konfeti resmin ağırlık merkezinden: hayvanın üstünden patlıyor.
        var cx = picture.Dots.Average(d => d.X);
        var cy = picture.Dots.Average(d => d.Y);

        _particles.Burst(
            ToScreen(new DotPoint(cx, cy)),
            _side * 0.10f,
            PloofyPalette.Sunny,
            _rng,
            count: 40);
    }

    private void Spark(DotPoint dot, HuePaint hue, int count) =>
        _particles.Burst(ToScreen(dot), _side * 0.04f, hue, _rng, count);

    private SKPoint ToScreen(DotPoint point) =>
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

        if (_finishedPicture is { } finished)
        {
            DrawFinished(canvas, finished);
            _particles.Draw(canvas);
            return;
        }

        DrawInProgress(canvas, round);
        _particles.Draw(canvas);
    }

    /// <summary>
    /// Birim kareyi ekrana oturtur.
    /// </summary>
    /// <remarks>
    /// Üstte bilgi şeridi için pay bırakılıyor: kare tam ortalanınca resmin
    /// üst noktaları şeridin altında kalıyor ve çocuk oraya parmak koyamıyor.
    /// </remarks>
    private void Layout(float width, float height)
    {
        var top = height * 0.11f;
        _side = MathF.Min(width * 0.92f, (height - top) * 0.92f);
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

    /// <summary>Yarısı çizilmiş resim: bağlanan çizgi, noktalar, rakamlar.</summary>
    private void DrawInProgress(SKCanvas canvas, DotToDotRound round)
    {
        var picture = round.Current;
        var radius = round.Tolerance * _side * 0.5f;

        DrawConnected(canvas, picture, round.Connected, isClosed: false, radius);

        for (var i = 0; i < picture.Count; i++)
        {
            var isDone = i < round.Connected;
            var isNext = i == round.Connected;
            var isWrong = i == _wrongDot && _time - _wrongAt < WrongFlashDuration;

            DrawDot(canvas, picture.Dots[i], i + 1, radius, isDone, isNext, isWrong,
                highlightsNext: round.HighlightsNext);
        }
    }

    /// <summary>Bitmiş resim: hat kapalı, noktalar sönük, çizgi kalın.</summary>
    private void DrawFinished(SKCanvas canvas, DotPicture picture)
    {
        // Kapanma son yarım saniyede çiziliyor: çocuk hattın kapandığını
        // görüyor, bir anda kapanmış bulmuyor.
        var radius = _side * 0.03f;
        DrawConnected(canvas, picture, picture.Count, isClosed: true, radius);

        foreach (var dot in picture.Dots)
        {
            _dot.Color = SKColors.White.WithAlpha(180);
            canvas.DrawCircle(ToScreen(dot), radius * 0.34f, _dot);
        }
    }

    private void DrawConnected(
        SKCanvas canvas, DotPicture picture, int upTo, bool isClosed, float radius)
    {
        if (upTo < 2)
        {
            return;
        }

        using var path = new SKPath();
        path.MoveTo(ToScreen(picture.Dots[0]));
        for (var i = 1; i < upTo; i++)
        {
            path.LineTo(ToScreen(picture.Dots[i]));
        }

        if (isClosed)
        {
            path.Close();
        }

        var width = MathF.Max(4f, radius * 0.55f);

        // Dış kılıf çizgiyi zeminden ayırıyor.
        _line.Color = PloofyPalette.Ink.WithAlpha(40);
        _line.StrokeWidth = width * 1.5f;
        canvas.DrawPath(path, _line);

        _line.Color = PloofyPalette.Cherry.Body;
        _line.StrokeWidth = width;
        canvas.DrawPath(path, _line);

        _line.Color = PloofyPalette.Cherry.Light.WithAlpha(150);
        _line.StrokeWidth = width * 0.4f;
        canvas.DrawPath(path, _line);
    }

    private void DrawDot(
        SKCanvas canvas,
        DotPoint dot,
        int number,
        float radius,
        bool isDone,
        bool isNext,
        bool isWrong,
        bool highlightsNext)
    {
        var center = ToScreen(dot);

        // Sıradaki nokta nabız gibi atıyor — ama yalnızca belirtme açıkken.
        // Meşe'de kapalı: orada oyun gerçekten rakam okumak.
        var shouldPulse = isNext && highlightsNext;
        var pulse = shouldPulse ? 1f + (0.18f * MathF.Sin(_time * 5.2f)) : 1f;

        var hue = isWrong
            ? PloofyPalette.Cherry
            : isDone
                ? PloofyPalette.Lime
                : shouldPulse
                    ? PloofyPalette.Sunny
                    : PloofyPalette.Ocean;

        if (shouldPulse)
        {
            // Halka: nabzı daireden büyük bir alanda göstermek, ekranın
            // uzağından bakan çocuğun da görmesini sağlıyor.
            _dot.Color = hue.Light.WithAlpha(90);
            canvas.DrawCircle(center, radius * 1.55f * pulse, _dot);
        }

        _dot.Color = isWrong ? hue.Shade : SKColors.White.WithAlpha(235);
        canvas.DrawCircle(center, radius * pulse, _dot);

        _dot.Color = hue.Body;
        canvas.DrawCircle(center, radius * 0.80f * pulse, _dot);

        DrawNumber(canvas, center, number, radius * pulse, isDone);
    }

    /// <summary>
    /// Noktanın rakamı.
    /// </summary>
    /// <remarks>
    /// Rakam oyunun kendisi, süsü değil — bu yüzden dairenin içine sığdığı
    /// en büyük boyutta çiziliyor. Taban çizgisine göre değil gerçek
    /// yükseklikle ortalanıyor: yoksa 1 ile 8 aynı dairede farklı
    /// yüksekliklerde duruyor.
    /// </remarks>
    private void DrawNumber(SKCanvas canvas, SKPoint center, int number, float radius, bool isDone)
    {
        var text = number.ToString(CultureInfo.InvariantCulture);

        // İki basamaklı sayı dairede daha az yer buluyor.
        _numberFont.Size = radius * (text.Length > 1 ? 0.95f : 1.20f);
        _numberFont.Embolden = true;

        var metrics = _numberFont.Metrics;
        var baseline = center.Y - ((metrics.Ascent + metrics.Descent) / 2f);

        _number.Color = isDone
            ? SKColors.White.WithAlpha(170)
            : SKColors.White;

        canvas.DrawText(text, center.X, baseline, SKTextAlign.Center, _numberFont, _number);
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
        _line.Dispose();
        _dot.Dispose();
        _number.Dispose();
        _numberFont.Dispose();
    }
}
