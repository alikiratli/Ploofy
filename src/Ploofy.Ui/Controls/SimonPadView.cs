using Ploofy.Ui.Painting;

namespace Ploofy.Ui.Controls;

/// <summary>Tuşun o andaki durumu.</summary>
public enum SimonPadState
{
    /// <summary>Sönük — bekliyor.</summary>
    Idle,

    /// <summary>Yanık — gösterimde ya da dokunulduğu anda.</summary>
    Lit,

    /// <summary>Yanlış tuş.</summary>
    Wrong,
}

/// <summary>
/// Sırayı Tekrarla'nın tek tuşu.
/// </summary>
/// <remarks>
/// <para>
/// Tuş rengin yanında bir <b>şekil</b> de taşıyor. Sebebi iki tane: renk körü
/// bir çocuk için dizi yalnızca renkle anlatılamaz, ve iki kanal (renk +
/// şekil) diziyi tek kanaldan daha akılda kalıcı yapıyor. Şekiller Şekil
/// Ayırma'nın sözlüğünden geliyor, yani çocuk onları zaten tanıyor.
/// </para>
/// <para>
/// Yanma bir renk değişimi değil bir <b>hareket</b>: tuş büyüyüp parlıyor.
/// Sönük ile yanık arasındaki farkın uzaktan da görülmesi gerekiyor, çünkü
/// çocuk gösterim sırasında ekranın tamamına bakıyor.
/// </para>
/// </remarks>
public sealed class SimonPadView : ContentView
{
    private readonly Border _border;
    private readonly Label _symbol;

    public SimonPadView()
    {
        _symbol = new Label
        {
            FontSize = 46,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        _border = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 28 },
            StrokeThickness = 3,
            Padding = 0,
            Shadow = new Shadow
            {
                Brush = Color.FromArgb("#40402A1E"),
                Offset = new Point(0, 6),
                Radius = 14,
                Opacity = 0.6f,
            },
            Content = _symbol,
        };

        Content = _border;
        Refresh();
    }

    public static readonly BindableProperty SymbolProperty = BindableProperty.Create(
        nameof(Symbol), typeof(string), typeof(SimonPadView), string.Empty,
        propertyChanged: (b, _, n) => ((SimonPadView)b)._symbol.Text = (string)n);

    /// <summary>Tuşun rengini seçen sayı — tuş sırası veriliyor.</summary>
    public static readonly BindableProperty HueIndexProperty = BindableProperty.Create(
        nameof(HueIndex), typeof(int), typeof(SimonPadView), 0,
        propertyChanged: (b, _, _) => ((SimonPadView)b).Refresh());

    public static readonly BindableProperty StateProperty = BindableProperty.Create(
        nameof(State), typeof(SimonPadState), typeof(SimonPadView), SimonPadState.Idle,
        propertyChanged: (b, o, n) =>
            ((SimonPadView)b).OnStateChanged((SimonPadState)o, (SimonPadState)n));

    public string Symbol
    {
        get => (string)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public int HueIndex
    {
        get => (int)GetValue(HueIndexProperty);
        set => SetValue(HueIndexProperty, value);
    }

    public SimonPadState State
    {
        get => (SimonPadState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private async void OnStateChanged(SimonPadState was, SimonPadState now)
    {
        Refresh();

        if (was == now)
        {
            return;
        }

        try
        {
            switch (now)
            {
                case SimonPadState.Lit:
                    // Süre görünüm modelinden geliyor; buradaki hareket
                    // yalnızca yanmanın kendisi, uzunluğu değil.
                    await _border.ScaleToAsync(1.08, 90, Easing.CubicOut);
                    break;

                case SimonPadState.Wrong:
                    await ShakeAsync();
                    break;

                default:
                    _border.TranslationX = 0;
                    await _border.ScaleToAsync(1.0, 130, Easing.CubicOut);
                    break;
            }
        }
        catch (TaskCanceledException)
        {
            // Sayfa kapanırken animasyon iptal olabiliyor.
        }
    }

    private async Task ShakeAsync()
    {
        const uint step = 55;
        await _border.TranslateToAsync(-10, 0, step, Easing.CubicOut);
        await _border.TranslateToAsync(10, 0, step, Easing.CubicInOut);
        await _border.TranslateToAsync(-6, 0, step, Easing.CubicInOut);
        await _border.TranslateToAsync(0, 0, step, Easing.CubicIn);
    }

    private void Refresh()
    {
        var palette = PloofyPalette.All;
        var hue = palette[((HueIndex % palette.Count) + palette.Count) % palette.Count];
        var isLit = State == SimonPadState.Lit;

        _border.Background = isLit
            // Yanık: ışığı üstten alan parlak yüz.
            ? Gradient(hue.Light, hue.Body)
            // Sönük: aynı renk ama gövdeden gölgeye — tuş kaybolmuyor,
            // yalnızca geri çekiliyor.
            : Gradient(hue.Body, hue.Shade);

        _border.Opacity = isLit ? 1.0 : 0.62;
        _border.Stroke = Colors.White.WithAlpha(isLit ? 0.95f : 0.35f);
        _symbol.TextColor = Colors.White.WithAlpha(isLit ? 1.0f : 0.62f);
    }

    private static Brush Gradient(SkiaSharp.SKColor from, SkiaSharp.SKColor to) =>
        new LinearGradientBrush(
            [new GradientStop(ToMaui(from), 0f), new GradientStop(ToMaui(to), 1f)],
            new Point(0, 0),
            new Point(1, 1));

    private static Color ToMaui(SkiaSharp.SKColor color) =>
        Color.FromRgba(color.Red, color.Green, color.Blue, color.Alpha);
}
