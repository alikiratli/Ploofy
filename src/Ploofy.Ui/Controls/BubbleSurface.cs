using Ploofy.Engine.Games;
using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>Bir balona dokunmanın ekrandaki karşılığı.</summary>
public sealed class BubbleTouchEventArgs(PopOutcome outcome, BubbleHue hue) : EventArgs
{
    public PopOutcome Outcome { get; } = outcome;

    public BubbleHue Hue { get; } = hue;
}

/// <summary>
/// Balon Patlatma'nın çizim yüzeyi.
/// </summary>
/// <remarks>
/// <para>
/// Kuralları <see cref="BubblePopRound"/> yürütüyor; buradaki tek iş onu
/// ekranda canlı göstermek. Yüzeyin oyunla ilgili hiçbir kararı yok — kaç
/// balon, hangi renk, ne kadar süre hep motorun sorusu.
/// </para>
/// <para>
/// Canlılık üç küçük ayrıntıdan geliyor ve üçü de bilinçli:
/// balonlar yerinde <b>belirmiyor</b>, esneyerek büyüyor; duruyorken hafifçe
/// nefes alıyor; yanlış renge dokunulunca patlamak yerine <b>silkeleniyor</b>.
/// Üçü olmadan aynı oyun "çalışıyor" ama ölü görünüyor.
/// </para>
/// </remarks>
public sealed class BubbleSurface : SKCanvasView
{
    private const float BirthDuration = 0.32f;
    private const float ShakeDuration = 0.4f;

    private readonly BubblePainter _painter = new();
    private readonly ParticleField _particles = new() { Gravity = 620f };
    private readonly Random _rng = new();

    private readonly Dictionary<int, float> _bornAt = [];
    private readonly Dictionary<int, float> _shakenAt = [];

    private readonly SKPaint _skyPaint = new() { IsAntialias = true };
    private readonly SKPaint _cloudPaint = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private IDispatcherTimer? _ticker;
    private DateTime _lastFrame;
    private float _time;

    public BubbleSurface()
    {
        EnableTouchEvents = true;
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
        Touch += OnTouch;
    }

    /// <summary>Çizilen tur. Değiştirildiğinde animasyon durumu sıfırlanıyor.</summary>
    public BubblePopRound? Round { get; private set; }

    /// <summary>Bir balona (ya da boşluğa) dokunuldu.</summary>
    public event EventHandler<BubbleTouchEventArgs>? Touched;

    /// <summary>Tur bitti — hedefe ulaşıldı ya da süre doldu.</summary>
    public event EventHandler? RoundOver;

    /// <summary>
    /// Her karede tetiklenir. Sayaç gibi zamana bağlı göstergeler kendi
    /// zamanlayıcılarını kurmak yerine buna bağlanıyor — iki ayrı saat
    /// birbirinden kayıyor ve ekranda tutarsız görünüyor.
    /// </summary>
    public event EventHandler? FrameRendered;

    public void Start(BubblePopRound round)
    {
        Round = round;
        _time = 0f;
        _bornAt.Clear();
        _shakenAt.Clear();
        _particles.Clear();

        _lastFrame = DateTime.UtcNow;
        StartTicker();
        InvalidateSurface();
    }

    public void Pause() => _ticker?.Stop();

    public void Resume()
    {
        if (Round is null || _ticker is null)
        {
            return;
        }

        _lastFrame = DateTime.UtcNow;
        _ticker.Start();
    }

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
        // sınırlamazsak balonlar bir anda ekranı terk ediyor.
        var delta = MathF.Min((float)(now - _lastFrame).TotalSeconds, 0.05f);
        _lastFrame = now;
        _time += delta;

        var round = Round;
        if (round is null)
        {
            return;
        }

        var wasOver = round.IsOver;
        round.Advance(TimeSpan.FromSeconds(delta));
        _particles.Advance(delta);

        FrameRendered?.Invoke(this, EventArgs.Empty);

        if (!wasOver && round.IsOver)
        {
            RoundOver?.Invoke(this, EventArgs.Empty);
        }

        InvalidateSurface();
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        e.Handled = true;

        if (e.ActionType != SKTouchAction.Pressed || Round is not { } round || round.IsOver)
        {
            return;
        }

        var width = CanvasSize.Width;
        var height = CanvasSize.Height;
        if (width <= 0 || height <= 0)
        {
            return;
        }

        var x = e.Location.X / width;
        var y = e.Location.Y / height;

        // Dokunulan balonu, patlamadan önce bulmamız gerekiyor: patlama
        // efektini onun rengiyle ve yerinde çizeceğiz.
        var hit = FindBubble(round, x, y);
        var outcome = round.PopAt(x, y);

        switch (outcome)
        {
            case PopOutcome.Popped when hit is not null:
                _particles.Burst(
                    new SKPoint(hit.X * width, hit.Y * height),
                    hit.Radius * width,
                    PloofyPalette.For(hit.Hue),
                    _rng);
                _bornAt.Remove(hit.Id);
                break;

            case PopOutcome.WrongColor when hit is not null:
                // Yanlış renk patlamıyor, silkeleniyor: patlasaydı yanlış da
                // bir ödül olurdu.
                _shakenAt[hit.Id] = _time;
                break;
        }

        Touched?.Invoke(this, new BubbleTouchEventArgs(outcome, hit?.Hue ?? default));
        InvalidateSurface();
    }

    private static Bubble? FindBubble(BubblePopRound round, float x, float y)
    {
        for (var i = round.Bubbles.Count - 1; i >= 0; i--)
        {
            var bubble = round.Bubbles[i];
            var dx = x - bubble.X;
            var dy = y - bubble.Y;
            var reach = bubble.Radius * 1.25f;

            if ((dx * dx) + (dy * dy) <= reach * reach)
            {
                return bubble;
            }
        }

        return null;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var width = e.Info.Width;
        var height = e.Info.Height;

        DrawSky(canvas, width, height);

        if (Round is { } round)
        {
            DrawBubbles(canvas, round, width, height);
        }

        _particles.Draw(canvas);
    }

    private void DrawSky(SKCanvas canvas, float width, float height)
    {
        _skyPaint.Shader?.Dispose();
        _skyPaint.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, height),
            [PloofyPalette.SkyTop, PloofyPalette.SkyMiddle, PloofyPalette.SkyBottom],
            [0f, 0.45f, 1f],
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, width, height, _skyPaint);

        // Yavaşça süzülen yumuşak lekeler. Düz bir zemin üstünde yükselen
        // balonların hareketi algılanmıyor; bu lekeler derinlik veriyor.
        _cloudPaint.Color = SKColors.White.WithAlpha(64);
        for (var i = 0; i < 4; i++)
        {
            var phase = _time * (0.05f + (i * 0.015f));
            var cx = ((0.18f + (i * 0.24f) + phase) % 1.25f - 0.12f) * width;
            var cy = height * (0.16f + (i % 2 == 0 ? 0.1f : 0.55f));
            var rx = width * (0.28f + (i * 0.05f));
            canvas.DrawOval(cx, cy, rx, rx * 0.42f, _cloudPaint);
        }
    }

    private void DrawBubbles(SKCanvas canvas, BubblePopRound round, float width, float height)
    {
        foreach (var bubble in round.Bubbles)
        {
            if (!_bornAt.TryGetValue(bubble.Id, out var born))
            {
                born = _time;
                _bornAt[bubble.Id] = born;
            }

            var age = _time - born;
            var scale = BirthScale(age);
            // Nefes alma. Her balonun kendi faz kayması var, yoksa hepsi aynı
            // anda şişip inerek mekanik görünüyor.
            var squash = 0.035f * MathF.Sin((_time * 2.1f) + bubble.AnimationOffset);

            var cx = bubble.X * width;
            var cy = bubble.Y * height;

            if (_shakenAt.TryGetValue(bubble.Id, out var shaken))
            {
                var shakeAge = _time - shaken;
                if (shakeAge > ShakeDuration)
                {
                    _shakenAt.Remove(bubble.Id);
                }
                else
                {
                    // Sönümlenen salınım: sert başlayıp yumuşayarak duruyor.
                    var damping = 1f - (shakeAge / ShakeDuration);
                    cx += MathF.Sin(shakeAge * 46f) * bubble.Radius * width * 0.28f * damping;
                }
            }

            _painter.Draw(
                canvas,
                new SKPoint(cx, cy),
                bubble.Radius * width,
                PloofyPalette.For(bubble.Hue),
                scale,
                squash);
        }

        // Ekranda olmayan balonların animasyon kaydı birikmesin.
        if (_bornAt.Count > round.Bubbles.Count * 3)
        {
            var alive = round.Bubbles.Select(b => b.Id).ToHashSet();
            foreach (var id in _bornAt.Keys.Where(id => !alive.Contains(id)).ToList())
            {
                _bornAt.Remove(id);
            }
        }
    }

    /// <summary>
    /// Doğuş animasyonu: hedefin biraz üstüne çıkıp geri oturan bir yay.
    /// </summary>
    /// <remarks>
    /// Doğrusal büyüme "beliriyor" gibi duruyor; hafif taşma "pat diye
    /// çıkıyor" hissi veriyor ve balonun canlı olduğunu anlatan ilk işaret bu.
    /// </remarks>
    private static float BirthScale(float age)
    {
        if (age >= BirthDuration)
        {
            return 1f;
        }

        var t = age / BirthDuration;
        return 1f + (0.28f * MathF.Sin(t * MathF.PI)) - (1f - t) * (1f - t);
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
        _skyPaint.Shader?.Dispose();
        _skyPaint.Dispose();
        _cloudPaint.Dispose();
    }
}
