using Ploofy.Engine.Games;
using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>Bir nesnenin akıbetinin ekrandaki karşılığı.</summary>
public sealed class BasketCatchEventArgs(bool caught, BubbleHue hue) : EventArgs
{
    public bool Caught { get; } = caught;

    public BubbleHue Hue { get; } = hue;
}

/// <summary>
/// Sepeti Tut'un çizim yüzeyi.
/// </summary>
/// <remarks>
/// <para>
/// Sepet <b>ekranın herhangi bir yerine</b> dokunulunca parmağı takip ediyor;
/// sepetin kendisini tutmak gerekmiyor. Küçük çocuk hareket eden küçük bir
/// hedefi güvenilir biçimde yakalayamıyor, ve sepeti tutmaya çalışırken
/// düşen nesneye bakamıyor — oysa oyunun tamamı o nesneye bakmakla ilgili.
/// </para>
/// <para>
/// Sepetin çizildiği yükseklik motordaki <see cref="BasketCatchRound.CatchLine"/>
/// ile aynı. İki yerde ayrı ayrı belirlenirse ekranda sepete değen bir nesne
/// motorda ıskalanmış sayılıyor.
/// </para>
/// </remarks>
public sealed class BasketCatchSurface : SKCanvasView
{
    private static readonly SKColor WickerLight = new(0xE8, 0xB4, 0x6B);
    private static readonly SKColor WickerBody = new(0xC9, 0x84, 0x3C);
    private static readonly SKColor WickerShade = new(0x8E, 0x54, 0x1E);
    private static readonly SKColor GroundTop = new(0x9E, 0xDB, 0x83);
    private static readonly SKColor GroundBottom = new(0x64, 0xAE, 0x55);

    /// <summary>Yakalama anındaki sepet zıplamasının süresi.</summary>
    private const float BounceDuration = 0.26f;

    private readonly ShapePainter _painter = new();
    private readonly ParticleField _particles = new() { Gravity = 720f };
    private readonly Random _rng = new();

    private readonly SKPaint _sky = new() { IsAntialias = true };
    private readonly SKPaint _ground = new() { IsAntialias = true };
    private readonly SKPaint _basket = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _weave = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    private IDispatcherTimer? _ticker;
    private DateTime _lastFrame;
    private float _time;
    private float _bouncedAt = float.MinValue;

    public BasketCatchSurface()
    {
        EnableTouchEvents = true;
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
        Touch += OnTouch;
    }

    public BasketCatchRound? Round { get; private set; }

    /// <summary>Bir nesne yakalandı ya da kaçtı.</summary>
    public event EventHandler<BasketCatchEventArgs>? Catch;

    /// <summary>Hedefe ulaşıldı.</summary>
    public event EventHandler? RoundOver;

    public void Start(BasketCatchRound round)
    {
        Round = round;
        _time = 0f;
        _bouncedAt = float.MinValue;
        _particles.Clear();

        _lastFrame = DateTime.UtcNow;
        StartTicker();
        InvalidateSurface();
    }

    public void Pause() => _ticker?.Stop();

    public void Stop()
    {
        _ticker?.Stop();
        _ticker = null;
    }

    private void StartTicker()
    {
        if (_ticker is not null)
        {
            _ticker.Start();
            return;
        }

        _ticker = Dispatcher.CreateTimer();
        _ticker.Interval = TimeSpan.FromMilliseconds(16);
        _ticker.Tick += (_, _) => Tick();
        _ticker.Start();
    }

    private void Tick()
    {
        var now = DateTime.UtcNow;
        // Uygulama arka plandan dönerken tek kare saatlerce sürmüş olabiliyor;
        // sınırlamazsak bütün nesneler bir karede yere iniyor.
        var delta = MathF.Min((float)(now - _lastFrame).TotalSeconds, 0.05f);
        _lastFrame = now;
        _time += delta;

        if (Round is not { } round)
        {
            return;
        }

        var wasComplete = round.IsComplete;
        round.Advance(TimeSpan.FromSeconds(delta));
        _particles.Advance(delta);

        var width = CanvasSize.Width;
        var height = CanvasSize.Height;

        foreach (var moment in round.LastEvents)
        {
            if (moment.Caught)
            {
                _bouncedAt = _time;
                _particles.Burst(
                    new SKPoint(moment.X * width, moment.Y * height),
                    round.ItemRadius * width,
                    PloofyPalette.For(moment.Hue),
                    _rng,
                    count: 14);
            }
            else
            {
                // Kaçan nesnenin yere çarpması: daha az ve daha alçak bir
                // toz. Görünmeden kaybolursa çocuk neyi kaçırdığını anlamıyor.
                _particles.Burst(
                    new SKPoint(moment.X * width, height * 0.97f),
                    round.ItemRadius * width * 0.6f,
                    PloofyPalette.For(moment.Hue),
                    _rng,
                    count: 6);
            }

            Catch?.Invoke(this, new BasketCatchEventArgs(moment.Caught, moment.Hue));
        }

        if (!wasComplete && round.IsComplete)
        {
            RoundOver?.Invoke(this, EventArgs.Empty);
        }

        InvalidateSurface();
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        e.Handled = true;

        if (Round is not { } round || round.IsComplete)
        {
            return;
        }

        var width = CanvasSize.Width;
        if (width <= 0)
        {
            return;
        }

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed or SKTouchAction.Moved:
                round.MoveBasketTo(e.Location.X / width);
                InvalidateSurface();
                break;
        }
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var width = e.Info.Width;
        var height = e.Info.Height;

        DrawSky(canvas, width, height);
        DrawGround(canvas, width, height);

        if (Round is not { } round)
        {
            return;
        }

        DrawItems(canvas, round, width, height);
        DrawBasket(canvas, round, width, height);

        _particles.Draw(canvas);
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

    /// <summary>
    /// Alttaki çim şeridi.
    /// </summary>
    /// <remarks>
    /// Süs değil ölçek: düşen nesnenin ne kadar yaklaştığı, ancak bir zemin
    /// varsa okunuyor. Zeminsiz gökyüzünde nesne boşlukta asılı duruyor.
    /// </remarks>
    private void DrawGround(SKCanvas canvas, float width, float height)
    {
        var top = height * 0.9f;

        _ground.Shader?.Dispose();
        _ground.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, top),
            new SKPoint(0, height),
            [GroundTop, GroundBottom],
            [0f, 1f],
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, top, width, height - top, _ground);
    }

    private void DrawItems(SKCanvas canvas, BasketCatchRound round, float width, float height)
    {
        foreach (var item in round.Items)
        {
            var radius = item.Radius * width;

            canvas.Save();
            canvas.Translate(item.X * width, item.Y * height);
            // Düşerken hafifçe dönüyor: dümdüz inen nesne kesilip yapıştırılmış
            // gibi duruyor, dönen nesne düşüyor gibi.
            canvas.RotateDegrees(item.Spin * item.Y * 180f);

            _painter.Draw(canvas, SKPoint.Empty, radius, item.Kind, PloofyPalette.For(item.Hue));

            canvas.Restore();
        }
    }

    private void DrawBasket(SKCanvas canvas, BasketCatchRound round, float width, float height)
    {
        var centerX = round.BasketX * width;
        var mouthY = BasketCatchRound.CatchLine * height;
        var halfWidth = round.BasketWidth / 2f * width;
        var basketHeight = halfWidth * 0.95f;

        // Yakalama anında kısa bir zıplama: sepetin bir şey aldığı, sayaçtan
        // önce sepetin kendisinden okunuyor.
        var bounceAge = _time - _bouncedAt;
        var squash = bounceAge < BounceDuration
            ? 0.12f * MathF.Sin(bounceAge / BounceDuration * MathF.PI)
            : 0f;

        var mouthHalf = halfWidth * (1f + squash);
        var bodyHeight = basketHeight * (1f - (squash * 0.5f));
        var footHalf = mouthHalf * 0.72f;

        // Gövde: ağzı geniş, tabanı dar bir yamuk.
        using var body = new SKPath();
        body.MoveTo(centerX - mouthHalf, mouthY);
        body.LineTo(centerX + mouthHalf, mouthY);
        body.LineTo(centerX + footHalf, mouthY + bodyHeight - (footHalf * 0.35f));
        body.QuadTo(
            centerX + footHalf,
            mouthY + bodyHeight,
            centerX + (footHalf * 0.72f),
            mouthY + bodyHeight);
        body.LineTo(centerX - (footHalf * 0.72f), mouthY + bodyHeight);
        body.QuadTo(
            centerX - footHalf,
            mouthY + bodyHeight,
            centerX - footHalf,
            mouthY + bodyHeight - (footHalf * 0.35f));
        body.Close();

        _basket.Shader?.Dispose();
        _basket.Shader = SKShader.CreateLinearGradient(
            new SKPoint(centerX - mouthHalf, mouthY),
            new SKPoint(centerX + mouthHalf, mouthY + bodyHeight),
            [WickerLight, WickerBody, WickerShade],
            [0f, 0.45f, 1f],
            SKShaderTileMode.Clamp);
        canvas.DrawPath(body, _basket);

        // Örgü: gövdeyi düz bir renk lekesi olmaktan çıkaran iki yatay çizgi.
        canvas.Save();
        canvas.ClipPath(body, antialias: true);
        _weave.Color = WickerShade.WithAlpha(70);
        _weave.StrokeWidth = MathF.Max(2f, bodyHeight * 0.055f);
        for (var i = 1; i <= 2; i++)
        {
            var y = mouthY + (bodyHeight * i / 3f);
            canvas.DrawLine(centerX - mouthHalf, y, centerX + mouthHalf, y, _weave);
        }

        canvas.Restore();

        // Ağız halkası gövdeden sonra çiziliyor: sepetin içi boş görünüyor
        // ve nesnenin "içine düştüğü" yer belli oluyor.
        _basket.Shader?.Dispose();
        _basket.Shader = null;
        _basket.Color = WickerLight;

        var rimHeight = bodyHeight * 0.28f;
        canvas.DrawOval(centerX, mouthY, mouthHalf, rimHeight / 2f, _basket);

        _basket.Color = WickerShade.WithAlpha(120);
        canvas.DrawOval(centerX, mouthY, mouthHalf * 0.86f, rimHeight * 0.34f, _basket);
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
        _ground.Shader?.Dispose();
        _basket.Shader?.Dispose();
        _sky.Dispose();
        _ground.Dispose();
        _basket.Dispose();
        _weave.Dispose();
    }
}
