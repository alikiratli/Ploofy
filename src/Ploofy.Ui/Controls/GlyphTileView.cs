using Ploofy.Ui.Painting;

namespace Ploofy.Ui.Controls;

/// <summary>Kutucuğun geri bildirim durumu.</summary>
public enum GlyphTileState
{
    Idle,
    Correct,
    Wrong,
}

/// <summary>
/// Üstünde tek bir harf ya da sayı olan dokunulabilir kutucuk.
/// </summary>
/// <remarks>
/// <para>
/// Harf/Sayı Avı ve ileride Say ve Eşleştir bunu paylaşacak. Renk balon ve
/// şekil paletinden geliyor: uygulamanın her yerinde aynı altı renk dolaşıyor.
/// </para>
/// <para>
/// Doğru ve yanlış geri bildirimi kutucuğun <b>kendi içinde</b>: veri şablonu
/// içinde animasyon yürütmenin temiz bir yolu yok ve bu geri bildirim,
/// çocuğun neyi yanlış yaptığını anladığı tek an.
/// </para>
/// </remarks>
public sealed class GlyphTileView : ContentView
{
    private readonly Border _border;
    private readonly Label _label;

    public GlyphTileView()
    {
        _label = new Label
        {
            FontSize = 54,
            FontAttributes = FontAttributes.Bold,
            TextColor = Colors.White,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        _border = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 26 },
            Stroke = Colors.White.WithAlpha(0.5f),
            StrokeThickness = 2,
            Padding = 0,
            Shadow = new Shadow
            {
                Brush = Color.FromArgb("#40402A1E"),
                Offset = new Point(0, 6),
                Radius = 14,
                Opacity = 0.6f,
            },
            Content = _label,
        };

        Content = _border;
        RefreshBackground();
    }

    public static readonly BindableProperty GlyphProperty = BindableProperty.Create(
        nameof(Glyph), typeof(string), typeof(GlyphTileView), string.Empty,
        propertyChanged: (b, _, n) => ((GlyphTileView)b)._label.Text = (string)n);

    /// <summary>
    /// Kutucuğun rengini seçen sayı. Aynı soruda yan yana gelen kutucuklar
    /// farklı renk alsın diye seçenek sırası veriliyor.
    /// </summary>
    public static readonly BindableProperty HueIndexProperty = BindableProperty.Create(
        nameof(HueIndex), typeof(int), typeof(GlyphTileView), 0,
        propertyChanged: (b, _, _) => ((GlyphTileView)b).RefreshBackground());

    public static readonly BindableProperty StateProperty = BindableProperty.Create(
        nameof(State), typeof(GlyphTileState), typeof(GlyphTileView), GlyphTileState.Idle,
        propertyChanged: (b, _, n) => ((GlyphTileView)b).OnStateChanged((GlyphTileState)n));

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public int HueIndex
    {
        get => (int)GetValue(HueIndexProperty);
        set => SetValue(HueIndexProperty, value);
    }

    public GlyphTileState State
    {
        get => (GlyphTileState)GetValue(StateProperty);
        set => SetValue(StateProperty, value);
    }

    private async void OnStateChanged(GlyphTileState state)
    {
        RefreshBackground();

        try
        {
            switch (state)
            {
                case GlyphTileState.Correct:
                    // Doğru cevap büyüyüp yerine oturuyor.
                    await _border.ScaleTo(1.16, 120, Easing.CubicOut);
                    await _border.ScaleTo(1.0, 180, Easing.SpringOut);
                    break;

                case GlyphTileState.Wrong:
                    // Yanlış cevap silkeleniyor: "bu değil, tekrar bak".
                    await ShakeAsync();
                    break;

                default:
                    _border.TranslationX = 0;
                    _border.Scale = 1;
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
        await _border.TranslateTo(-10, 0, step, Easing.CubicOut);
        await _border.TranslateTo(10, 0, step, Easing.CubicInOut);
        await _border.TranslateTo(-6, 0, step, Easing.CubicInOut);
        await _border.TranslateTo(0, 0, step, Easing.CubicIn);
    }

    private void RefreshBackground()
    {
        if (State == GlyphTileState.Correct)
        {
            _border.Background = Gradient(PloofyPalette.Lime);
            return;
        }

        var palette = PloofyPalette.All;
        var hue = palette[((HueIndex % palette.Count) + palette.Count) % palette.Count];
        _border.Background = Gradient(hue);
    }

    private static Brush Gradient(HuePaint hue) => new LinearGradientBrush(
        [
            new GradientStop(ToMaui(hue.Light), 0f),
            new GradientStop(ToMaui(hue.Body), 0.55f),
            new GradientStop(ToMaui(hue.Shade), 1f),
        ],
        new Point(0, 0),
        new Point(1, 1));

    private static Color ToMaui(SkiaSharp.SKColor color) =>
        Color.FromRgba(color.Red, color.Green, color.Blue, color.Alpha);
}
