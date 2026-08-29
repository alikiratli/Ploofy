namespace Ploofy.Ui.Controls;

/// <summary>
/// Eşleştirme Kartları'nın tek kartı — çevirme animasyonuyla.
/// </summary>
/// <remarks>
/// <para>
/// Kartın açılması bir durum değişikliği değil, bir <b>hareket</b>: kart
/// gerçekten dönüyor. Sembolün bir anda belirmesi ile kartın çevrilmesi
/// arasındaki fark, bu yaş grubunda oyunun "gerçek" hissedilip
/// hissedilmemesini belirliyor.
/// </para>
/// <para>
/// Animasyon kontrolün içinde duruyor, görünüm modelinde değil: kart listesi
/// bir veri şablonundan üretiliyor ve şablon içinde animasyon yürütmenin
/// temiz bir yolu yok.
/// </para>
/// </remarks>
public sealed class MemoryCardView : ContentView
{
    private static readonly Color BackTop = Color.FromArgb("#7FD2FF");
    private static readonly Color BackBottom = Color.FromArgb("#2E8FD6");
    private static readonly Color FaceColor = Color.FromArgb("#FFFFFF");
    private static readonly Color MatchedTop = Color.FromArgb("#A8EFA0");
    private static readonly Color MatchedBottom = Color.FromArgb("#4CB963");

    private readonly Border _border;
    private readonly Label _symbol;
    private readonly Label _pattern;

    private bool _isFlipping;

    public MemoryCardView()
    {
        _symbol = new Label
        {
            FontSize = 44,
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
            IsVisible = false,
        };

        // Kapalı kartın deseni. Düz renkli arka yüz ucuz görünüyor; hafif bir
        // desen kartı "oyun kartı" yapıyor.
        _pattern = new Label
        {
            Text = "✦",
            FontSize = 34,
            TextColor = Colors.White.WithAlpha(0.45f),
            HorizontalOptions = LayoutOptions.Center,
            VerticalOptions = LayoutOptions.Center,
        };

        _border = new Border
        {
            StrokeShape = new Microsoft.Maui.Controls.Shapes.RoundRectangle { CornerRadius = 22 },
            Stroke = Colors.White.WithAlpha(0.55f),
            StrokeThickness = 2,
            Background = BackBrush(),
            Padding = 0,
            Shadow = new Shadow
            {
                Brush = Color.FromArgb("#40402A1E"),
                Offset = new Point(0, 5),
                Radius = 12,
                Opacity = 0.6f,
            },
            Content = new Grid { Children = { _pattern, _symbol } },
        };

        Content = _border;
    }

    public static readonly BindableProperty SymbolProperty = BindableProperty.Create(
        nameof(Symbol), typeof(string), typeof(MemoryCardView), string.Empty,
        propertyChanged: (b, _, _) => ((MemoryCardView)b).RefreshFace());

    public static readonly BindableProperty IsRevealedProperty = BindableProperty.Create(
        nameof(IsRevealed), typeof(bool), typeof(MemoryCardView), false,
        propertyChanged: (b, o, n) => ((MemoryCardView)b).OnRevealChanged((bool)o, (bool)n));

    public static readonly BindableProperty IsMatchedProperty = BindableProperty.Create(
        nameof(IsMatched), typeof(bool), typeof(MemoryCardView), false,
        propertyChanged: (b, o, n) => ((MemoryCardView)b).OnMatchedChanged((bool)o, (bool)n));

    public string Symbol
    {
        get => (string)GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public bool IsRevealed
    {
        get => (bool)GetValue(IsRevealedProperty);
        set => SetValue(IsRevealedProperty, value);
    }

    public bool IsMatched
    {
        get => (bool)GetValue(IsMatchedProperty);
        set => SetValue(IsMatchedProperty, value);
    }

    private async void OnRevealChanged(bool wasRevealed, bool isRevealed)
    {
        if (wasRevealed == isRevealed || _isFlipping)
        {
            RefreshFace();
            return;
        }

        _isFlipping = true;
        try
        {
            // Kartın yarısına kadar dön, yüzü değiştir, kalan yarıyı dön.
            // Yüzü tam ortada değiştirmek, arkanın hiç görünmemesini sağlıyor.
            await _border.RotateYToAsync(90, 110, Easing.CubicIn);
            RefreshFace();
            _border.RotationY = -90;
            await _border.RotateYToAsync(0, 130, Easing.CubicOut);
        }
        catch (TaskCanceledException)
        {
            // Sayfa kapanırken animasyon iptal olabiliyor; kart zaten gidiyor.
        }
        finally
        {
            _isFlipping = false;
        }
    }

    private async void OnMatchedChanged(bool wasMatched, bool isMatched)
    {
        RefreshFace();

        if (wasMatched || !isMatched)
        {
            return;
        }

        try
        {
            // Eşleşen çift kısa bir "zıplama" yapıyor: doğru cevabın
            // görsel ödülü bu.
            await _border.ScaleToAsync(1.14, 110, Easing.CubicOut);
            await _border.ScaleToAsync(1.0, 160, Easing.SpringOut);
        }
        catch (TaskCanceledException)
        {
        }
    }

    private void RefreshFace()
    {
        var faceUp = IsRevealed || IsMatched;

        _symbol.Text = Symbol;
        _symbol.IsVisible = faceUp;
        _pattern.IsVisible = !faceUp;

        _border.Background = IsMatched
            ? MatchedBrush()
            : faceUp
                ? new SolidColorBrush(FaceColor)
                : BackBrush();
    }

    private static Brush BackBrush() => new LinearGradientBrush(
        [new GradientStop(BackTop, 0f), new GradientStop(BackBottom, 1f)],
        new Point(0, 0),
        new Point(1, 1));

    private static Brush MatchedBrush() => new LinearGradientBrush(
        [new GradientStop(MatchedTop, 0f), new GradientStop(MatchedBottom, 1f)],
        new Point(0, 0),
        new Point(1, 1));
}
