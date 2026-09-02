using Ploofy.Engine.Games;
using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>Kutucuğun geri bildirim durumu.</summary>
public enum ShapeTileState
{
    Idle,
    Correct,
    Wrong,
}

/// <summary>
/// Tek bir şekli gösteren küçük kare.
/// </summary>
/// <remarks>
/// <para>
/// Örüntü Tamamlama'nın hem dizisi hem seçenekleri bundan kuruluyor.
/// Kutucuk kendi kendini çiziyor: aynı şekli hem dizide hem seçenekte
/// göstermenin başka yolu, iki ayrı çizim koduna aynı şekli iki kez
/// anlatmak olurdu.
/// </para>
/// <para>
/// Boş kutucuk hayalet çizimle gösteriliyor — kesik çizgili, içi boş.
/// Dolu çizilen bir boşluğu çocuk "zaten cevaplanmış" sanıyor.
/// </para>
/// </remarks>
public sealed class ShapeTileView : SKCanvasView
{
    private readonly ShapePainter _painter = new();

    private float _pop = 1f;
    private IDispatcherTimer? _animation;
    private float _animationTime;
    private bool _isShaking;

    public ShapeTileView()
    {
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
    }

    public static readonly BindableProperty KindProperty = BindableProperty.Create(
        nameof(Kind), typeof(ShapeKind), typeof(ShapeTileView), ShapeKind.Circle,
        propertyChanged: Redraw);

    public static readonly BindableProperty HueProperty = BindableProperty.Create(
        nameof(Hue), typeof(BubbleHue), typeof(ShapeTileView), BubbleHue.Cherry,
        propertyChanged: Redraw);

    /// <summary>Boşluk mu? Boşsa hayalet çiziliyor.</summary>
    public static readonly BindableProperty IsEmptyProperty = BindableProperty.Create(
        nameof(IsEmpty), typeof(bool), typeof(ShapeTileView), false,
        propertyChanged: Redraw);

    /// <summary>
    /// Kutucuğun dikkat çekmesi gerekiyor mu?
    /// </summary>
    /// <remarks>
    /// Boşluk nabız gibi atıyor: dizideki dokuz kutucuk arasında hangisinin
    /// eksik olduğunu, hareketsiz bir kesik çizgi yeterince söylemiyor.
    /// </remarks>
    public static readonly BindableProperty IsWaitingProperty = BindableProperty.Create(
        nameof(IsWaiting), typeof(bool), typeof(ShapeTileView), false,
        propertyChanged: (b, _, n) => ((ShapeTileView)b).OnWaitingChanged((bool)n));

    public ShapeKind Kind
    {
        get => (ShapeKind)GetValue(KindProperty);
        set => SetValue(KindProperty, value);
    }

    public BubbleHue Hue
    {
        get => (BubbleHue)GetValue(HueProperty);
        set => SetValue(HueProperty, value);
    }

    public bool IsEmpty
    {
        get => (bool)GetValue(IsEmptyProperty);
        set => SetValue(IsEmptyProperty, value);
    }

    public bool IsWaiting
    {
        get => (bool)GetValue(IsWaitingProperty);
        set => SetValue(IsWaitingProperty, value);
    }

    private static void Redraw(BindableObject bindable, object old, object now) =>
        ((ShapeTileView)bindable).InvalidateSurface();

    /// <summary>
    /// Doğru/yanlış geri bildirimi.
    /// </summary>
    /// <remarks>
    /// Kutucuğun <b>kendi içinde</b>: veri şablonu içinde animasyon
    /// yürütmenin temiz bir yolu yok ve bu geri bildirim, çocuğun neyi
    /// yanlış yaptığını anladığı tek an. Aynı çözüm
    /// <see cref="GlyphTileView"/>'da da var.
    /// </remarks>
    public static readonly BindableProperty StateProperty = BindableProperty.Create(
        nameof(State), typeof(ShapeTileState), typeof(ShapeTileView), ShapeTileState.Idle,
        propertyChanged: (b, _, n) => ((ShapeTileView)b).OnStateChanged((ShapeTileState)n));

    public ShapeTileState State
    {
        get => (ShapeTileState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private async void OnStateChanged(ShapeTileState state)
    {
        try
        {
            switch (state)
            {
                case ShapeTileState.Correct:
                    await this.ScaleToAsync(1.18, 120, Easing.CubicOut);
                    await this.ScaleToAsync(1.0, 180, Easing.SpringOut);
                    break;

                case ShapeTileState.Wrong:
                    await ShakeAsync();
                    break;

                default:
                    TranslationX = 0;
                    Scale = 1;
                    break;
            }
        }
        catch (TaskCanceledException)
        {
            // Sayfa kapanırken animasyon iptal olabiliyor.
        }
    }

    /// <summary>
    /// Yanlış seçim: silkeleniyor.
    /// </summary>
    /// <remarks>
    /// Yanlış olan kaybolmuyor, yalnızca "bu değil" diyor. Kaybolsaydı
    /// eleme yoluyla doğruyu bulmak mümkün olurdu ve oyun örüntüyü değil
    /// sabrı ölçerdi.
    /// </remarks>
    private async Task ShakeAsync()
    {
        if (_isShaking)
        {
            return;
        }

        _isShaking = true;
        try
        {
            const uint step = 55;
            await this.TranslateToAsync(-10, 0, step, Easing.CubicOut);
            await this.TranslateToAsync(10, 0, step, Easing.CubicInOut);
            await this.TranslateToAsync(-6, 0, step, Easing.CubicInOut);
            await this.TranslateToAsync(0, 0, step, Easing.CubicIn);
        }
        finally
        {
            _isShaking = false;
        }
    }

    private void OnWaitingChanged(bool isWaiting)
    {
        if (!isWaiting)
        {
            _animation?.Stop();
            _animation = null;
            _pop = 1f;
            InvalidateSurface();
            return;
        }

        _animation ??= CreateTicker();
        _animation.Start();
    }

    private IDispatcherTimer CreateTicker()
    {
        var timer = Dispatcher.CreateTimer();
        timer.Interval = TimeSpan.FromMilliseconds(33);
        timer.Tick += (_, _) =>
        {
            _animationTime += 0.033f;
            _pop = 1f + (0.07f * MathF.Sin(_animationTime * 3.2f));
            InvalidateSurface();
        };

        return timer;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        canvas.Clear();

        var center = new SKPoint(e.Info.Width / 2f, e.Info.Height / 2f);

        // Şeklin sığdığı daire kutucuğun kısa kenarına göre; kare olmayan bir
        // kutucukta bile şekil taşmıyor.
        var radius = MathF.Min(e.Info.Width, e.Info.Height) * 0.40f;

        if (IsEmpty)
        {
            _painter.DrawGhost(
                canvas, center, radius * _pop, Kind, PloofyPalette.Ink, isHighlighted: false);
            return;
        }

        _painter.Draw(canvas, center, radius, Kind, PloofyPalette.For(Hue));
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null)
        {
            return;
        }

        _animation?.Stop();
        _animation = null;
        _painter.Dispose();
    }
}
