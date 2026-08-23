using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>
/// Kutlama konfetisi.
/// </summary>
/// <remarks>
/// Sonuç ekranının üstüne saydam bir katman olarak konuyor ve dokunuşları
/// geçiriyor (<see cref="InputTransparent"/>), yani altındaki düğmeleri
/// engellemiyor. Konfeti bitince zamanlayıcı kendini durduruyor: arka planda
/// boşuna dönen bir çizim döngüsü pil yakıyor.
/// </remarks>
public sealed class ConfettiView : SKCanvasView
{
    private readonly ParticleField _particles = new() { Gravity = 260f };
    private readonly Random _rng = new();

    private IDispatcherTimer? _ticker;
    private DateTime _lastFrame;
    private bool _pending;

    public ConfettiView()
    {
        InputTransparent = true;
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
    }

    /// <summary>Konfetiyi başlatır. Yüzey boyutu bilinmiyorsa ilk çizimi bekler.</summary>
    public void Celebrate()
    {
        _pending = true;
        StartTicker();
        InvalidateSurface();
    }

    public void Stop()
    {
        _ticker?.Stop();
        _ticker = null;
        _particles.Clear();
        InvalidateSurface();
    }

    private void StartTicker()
    {
        if (_ticker is not null)
        {
            return;
        }

        _lastFrame = DateTime.UtcNow;
        _ticker = Dispatcher.CreateTimer();
        _ticker.Interval = TimeSpan.FromMilliseconds(16);
        _ticker.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            var delta = (float)(now - _lastFrame).TotalSeconds;
            _lastFrame = now;

            // Uygulama arka plandan dönerken tek karede saatler geçmiş
            // olabiliyor; sıçramayı kesiyoruz.
            _particles.Advance(MathF.Min(delta, 0.05f));

            if (_particles.IsEmpty && !_pending)
            {
                Stop();
                return;
            }

            InvalidateSurface();
        };
        _ticker.Start();
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear(SKColors.Transparent);

        if (_pending)
        {
            _pending = false;
            _particles.Confetti(e.Info.Width, e.Info.Height, _rng);
        }

        _particles.Draw(canvas);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            Stop();
            _particles.Dispose();
        }
    }
}
