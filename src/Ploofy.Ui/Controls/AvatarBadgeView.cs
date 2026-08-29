using Ploofy.Ui.Painting;

namespace Ploofy.Ui.Controls;

/// <summary>
/// Çocuğun avatarı — renkli bir rozetin içinde.
/// </summary>
/// <remarks>
/// <para>
/// Emoji tek başına ekranda yazı gibi duruyor; renkli bir daire onu bir
/// <b>kimlik</b> yapıyor. Okuma bilmeyen çocuk kendi profilini adından değil
/// bu rozetten tanıyor, o yüzden rozet süs değil işlevin kendisi.
/// </para>
/// <para>
/// Rozetin rengi emojiden türetiliyor: aynı avatar uygulamanın her yerinde
/// aynı renkte. Renk profille birlikte kaydedilmiyor, çünkü kaydedilseydi
/// eski profillerin rengi olmazdı ve palet değişince ekranlar birbirinden
/// ayrışırdı.
/// </para>
/// </remarks>
public sealed class AvatarBadgeView : ContentView
{
    private readonly Border _border;
    private readonly Label _glyph;

    public AvatarBadgeView()
    {
        _glyph = new Label
        {
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        _border = new Border
        {
            Padding = 0,
            StrokeThickness = 3,
            Content = _glyph,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        Content = _border;
        Refresh();
    }

    public static readonly BindableProperty AvatarProperty = BindableProperty.Create(
        nameof(Avatar), typeof(string), typeof(AvatarBadgeView), string.Empty,
        propertyChanged: (b, _, _) => ((AvatarBadgeView)b).Refresh());

    /// <summary>Rozetin çapı.</summary>
    public static readonly BindableProperty SizeProperty = BindableProperty.Create(
        nameof(Size), typeof(double), typeof(AvatarBadgeView), 64d,
        propertyChanged: (b, _, _) => ((AvatarBadgeView)b).Refresh());

    public static readonly BindableProperty IsSelectedProperty = BindableProperty.Create(
        nameof(IsSelected), typeof(bool), typeof(AvatarBadgeView), false,
        propertyChanged: (b, o, n) =>
            ((AvatarBadgeView)b).OnSelectionChanged((bool)o, (bool)n));

    public string Avatar
    {
        get => (string)GetValue(AvatarProperty);
        set => SetValue(AvatarProperty, value);
    }

    public double Size
    {
        get => (double)GetValue(SizeProperty);
        set => SetValue(SizeProperty, value);
    }

    public bool IsSelected
    {
        get => (bool)GetValue(IsSelectedProperty);
        set => SetValue(IsSelectedProperty, value);
    }

    private async void OnSelectionChanged(bool was, bool now)
    {
        Refresh();

        if (was == now || !now)
        {
            return;
        }

        try
        {
            // Seçilen rozet kısaca büyüyor: hangisine dokunulduğu, çerçeve
            // değişiminden daha hızlı okunuyor.
            await _border.ScaleToAsync(1.14, 110, Easing.CubicOut);
            await _border.ScaleToAsync(1.0, 160, Easing.SpringOut);
        }
        catch (TaskCanceledException)
        {
            // Sayfa kapanırken animasyon iptal olabiliyor.
        }
    }

    private void Refresh()
    {
        var size = Math.Max(24d, Size);

        _border.WidthRequest = size;
        _border.HeightRequest = size;
        _border.StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle
        {
            CornerRadius = new CornerRadius(size / 2d),
        };

        // Emoji dairenin içine sığmalı: yarıçapın tamamı kadar bir yazı
        // boyutu kenarlardan taşıyor.
        _glyph.FontSize = size * 0.52;
        _glyph.Text = Avatar;

        var hue = HueFor(Avatar);
        _border.Background = new LinearGradientBrush(
            [
                new GradientStop(ToMaui(hue.Light), 0f),
                new GradientStop(ToMaui(hue.Body), 0.6f),
                new GradientStop(ToMaui(hue.Shade), 1f),
            ],
            new Point(0, 0),
            new Point(1, 1));

        _border.Stroke = IsSelected ? Colors.White : Colors.White.WithAlpha(0.45f);
        _border.StrokeThickness = IsSelected ? size * 0.075 : 2;
        _border.Shadow = new Shadow
        {
            Brush = Color.FromArgb("#40402A1E"),
            Offset = new Point(0, IsSelected ? 6 : 4),
            Radius = IsSelected ? 16 : 10,
            Opacity = 0.6f,
        };
    }

    /// <summary>
    /// Emojiden renk seçer.
    /// </summary>
    /// <remarks>
    /// <c>string.GetHashCode()</c> <b>kullanılamaz</b>: .NET'te her süreçte
    /// farklı sonuç veriyor, yani çocuğun avatarı uygulamanın her açılışında
    /// başka renkte olurdu. Kod noktalarının toplamı kalıcı ve bu iş için
    /// yeterli.
    /// </remarks>
    private static HuePaint HueFor(string avatar)
    {
        var palette = PloofyPalette.All;
        if (string.IsNullOrEmpty(avatar))
        {
            return palette[0];
        }

        var sum = 0;
        foreach (var unit in avatar)
        {
            sum = (sum + unit) % 4096;
        }

        return palette[sum % palette.Count];
    }

    private static Color ToMaui(SkiaSharp.SKColor color) =>
        Color.FromRgba(color.Red, color.Green, color.Blue, color.Alpha);
}
