using Ploofy.Engine.Progress;
using Ploofy.Ui.Painting;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>
/// Günlük oyun süresinin sütun grafiği.
/// </summary>
/// <remarks>
/// Çizimin tamamı <see cref="TrendPainter"/> içinde ve MAUI'den bağımsız;
/// burası yalnızca bağlanabilir özellikleri tutup yüzeyi ona veriyor.
/// Ayrılığın sebebi, yerleşimin ekrana bakmadan doğrulanamaması: aynı çizim
/// küçük bir konsol programından PNG'ye alınıp gözle bakılabiliyor.
/// </remarks>
public sealed class PlayTrendChart : SKCanvasView
{
    private readonly TrendPainter _painter = new();

    public PlayTrendChart()
    {
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
    }

    public static readonly BindableProperty DaysProperty = BindableProperty.Create(
        nameof(Days), typeof(IReadOnlyList<ReportDay>), typeof(PlayTrendChart), null,
        propertyChanged: Redraw);

    /// <summary>
    /// Gün harfleri, <see cref="DayOfWeek"/> sırasıyla (Pazar'dan Cumartesi'ye).
    /// </summary>
    /// <remarks>
    /// Arayüz katmanı üç dili tanımıyor, bu yüzden harfler dışarıdan geliyor —
    /// ebeveyn kilidi metinlerindeki düzenin aynısı.
    /// </remarks>
    public static readonly BindableProperty WeekdayInitialsProperty = BindableProperty.Create(
        nameof(WeekdayInitials), typeof(IReadOnlyList<string>), typeof(PlayTrendChart), null,
        propertyChanged: Redraw);

    /// <summary>Tepe etiketinin birimi ("dk" / "min" / "Min.").</summary>
    public static readonly BindableProperty MinuteSuffixProperty = BindableProperty.Create(
        nameof(MinuteSuffix), typeof(string), typeof(PlayTrendChart), "min",
        propertyChanged: Redraw);

    public IReadOnlyList<ReportDay>? Days
    {
        get => (IReadOnlyList<ReportDay>?)GetValue(DaysProperty);
        set => SetValue(DaysProperty, value);
    }

    public IReadOnlyList<string>? WeekdayInitials
    {
        get => (IReadOnlyList<string>?)GetValue(WeekdayInitialsProperty);
        set => SetValue(WeekdayInitialsProperty, value);
    }

    public string MinuteSuffix
    {
        get => (string)GetValue(MinuteSuffixProperty);
        set => SetValue(MinuteSuffixProperty, value);
    }

    private static void Redraw(BindableObject bindable, object old, object now) =>
        ((PlayTrendChart)bindable).InvalidateSurface();

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        e.Surface.Canvas.Clear();

        if (Days is not { Count: > 0 } days)
        {
            return;
        }

        _painter.Draw(
            e.Surface.Canvas,
            e.Info.Width,
            e.Info.Height,
            days,
            WeekdayInitials ?? [],
            MinuteSuffix);
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is null)
        {
            _painter.Dispose();
        }
    }
}
