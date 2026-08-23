using Ploofy.Engine.Progress;

namespace Ploofy.Ui.Controls;

/// <summary>
/// Kazanılan yıldızları gösteren satır.
/// </summary>
/// <remarks>
/// Sayı yerine yıldız gösteriliyor: okuma bilmeyen çocuk "2/3" ifadesini
/// okuyamıyor ama iki dolu bir boş yıldızı anında anlıyor. Aynı kontrol hem
/// oyun kartında (küçük) hem sonuç ekranında (büyük) kullanılıyor.
/// </remarks>
public sealed class StarRow : HorizontalStackLayout
{
    public static readonly BindableProperty StarsProperty = BindableProperty.Create(
        nameof(Stars),
        typeof(int),
        typeof(StarRow),
        defaultValue: 0,
        propertyChanged: (bindable, _, _) => ((StarRow)bindable).Rebuild());

    public static readonly BindableProperty StarSizeProperty = BindableProperty.Create(
        nameof(StarSize),
        typeof(double),
        typeof(StarRow),
        defaultValue: 22d,
        propertyChanged: (bindable, _, _) => ((StarRow)bindable).Rebuild());

    public StarRow()
    {
        Spacing = 4;
        Rebuild();
    }

    /// <summary>Dolu yıldız sayısı (0-3).</summary>
    public int Stars
    {
        get => (int)GetValue(StarsProperty);
        set => SetValue(StarsProperty, value);
    }

    public double StarSize
    {
        get => (double)GetValue(StarSizeProperty);
        set => SetValue(StarSizeProperty, value);
    }

    private void Rebuild()
    {
        Children.Clear();

        var filled = Math.Clamp(Stars, 0, StarRating.MaxStars);

        for (var i = 0; i < StarRating.MaxStars; i++)
        {
            Children.Add(new Label
            {
                Text = "★",
                FontSize = StarSize,
                VerticalOptions = LayoutOptions.Center,
                TextColor = i < filled ? FilledColor : EmptyColor,
            });
        }
    }

    private static Color FilledColor =>
        Application.Current?.Resources.TryGetValue("StarFilled", out var value) == true
            ? (Color)value
            : Colors.Goldenrod;

    private static Color EmptyColor =>
        Application.Current?.Resources.TryGetValue("StarEmpty", out var value) == true
            ? (Color)value
            : Colors.LightGray;
}
