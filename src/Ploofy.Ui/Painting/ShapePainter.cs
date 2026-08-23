using Ploofy.Engine.Games;
using SkiaSharp;

namespace Ploofy.Ui.Painting;

/// <summary>
/// Şekilleri balonlarla aynı dille çizer.
/// </summary>
/// <remarks>
/// <para>
/// Katmanlar <see cref="BubblePainter"/> ile aynı: gölge, ışığı sol üstten
/// alan degrade gövde, ince kenar halkası, parlama. Aynı dil olması bilinçli —
/// iki oyun arasında geçen çocuk aynı dünyada kaldığını hissetmeli.
/// </para>
/// <para>
/// Yollar her karede değil, tür ve boyut başına bir kez üretilip
/// önbelleğe alınıyor: yıldız ve kalp yolları pahalı ve saniyede 60 kez
/// yeniden kurmanın anlamı yok.
/// </para>
/// </remarks>
public sealed class ShapePainter : IDisposable
{
    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _rim = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _shadow = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gloss = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private readonly Dictionary<(ShapeKind, int), SKPath> _paths = [];

    /// <summary>
    /// Şekli dolu olarak çizer.
    /// </summary>
    /// <param name="radius">Şeklin sığdığı dairenin yarıçapı.</param>
    public void Draw(
        SKCanvas canvas,
        SKPoint center,
        float radius,
        ShapeKind kind,
        HuePaint hue,
        float scale = 1f,
        byte alpha = 255)
    {
        if (radius <= 0f || scale <= 0f)
        {
            return;
        }

        var r = radius * scale;
        var path = PathFor(kind, r);

        canvas.Save();
        canvas.Translate(center.X, center.Y);

        _shadow.Color = new SKColor(0x2A, 0x1A, 0x3A, (byte)(alpha * 0.20f));
        _shadow.ImageFilter?.Dispose();
        _shadow.ImageFilter = SKImageFilter.CreateBlur(r * 0.18f, r * 0.18f);
        canvas.Save();
        canvas.Translate(0, r * 0.12f);
        canvas.DrawPath(path, _shadow);
        canvas.Restore();

        _fill.Shader?.Dispose();
        _fill.Shader = SKShader.CreateLinearGradient(
            new SKPoint(-r * 0.7f, -r),
            new SKPoint(r * 0.6f, r),
            [hue.Light.WithAlpha(alpha), hue.Body.WithAlpha(alpha), hue.Shade.WithAlpha(alpha)],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp);
        canvas.DrawPath(path, _fill);

        _rim.StrokeWidth = MathF.Max(1.5f, r * 0.055f);
        _rim.Color = hue.Light.WithAlpha((byte)(alpha * 0.8f));
        canvas.DrawPath(path, _rim);

        // Parlama: şeklin sol üst köşesine kırpılmış bir ışık lekesi.
        canvas.Save();
        canvas.ClipPath(path, antialias: true);
        _gloss.Color = SKColors.White.WithAlpha((byte)(alpha * 0.38f));
        canvas.DrawOval(-r * 0.34f, -r * 0.52f, r * 0.42f, r * 0.24f, _gloss);
        canvas.Restore();

        canvas.Restore();
    }

    /// <summary>
    /// Şekli kutu ağzı olarak çizer: içi boş, kesik çizgili bir hayalet.
    /// </summary>
    /// <remarks>
    /// Kutunun içi dolu çizilirse çocuk onu da sürüklenecek bir parça
    /// sanıyor. Hayalet çizim "buraya gelecek" diyor.
    /// </remarks>
    public void DrawGhost(
        SKCanvas canvas,
        SKPoint center,
        float radius,
        ShapeKind kind,
        SKColor color,
        bool isHighlighted)
    {
        var path = PathFor(kind, radius);

        canvas.Save();
        canvas.Translate(center.X, center.Y);

        if (isHighlighted)
        {
            // Parça kutunun üstündeyken içi hafifçe doluyor: "bırakırsan
            // buraya girer" geri bildirimi.
            _fill.Shader?.Dispose();
            _fill.Shader = null;
            _fill.Color = color.WithAlpha(70);
            canvas.DrawPath(path, _fill);
        }

        _rim.StrokeWidth = MathF.Max(2f, radius * (isHighlighted ? 0.10f : 0.07f));
        _rim.Color = color.WithAlpha(isHighlighted ? (byte)255 : (byte)150);
        _rim.PathEffect?.Dispose();
        _rim.PathEffect = isHighlighted
            ? null
            : SKPathEffect.CreateDash([radius * 0.28f, radius * 0.18f], 0);
        canvas.DrawPath(path, _rim);
        _rim.PathEffect?.Dispose();
        _rim.PathEffect = null;

        canvas.Restore();
    }

    private SKPath PathFor(ShapeKind kind, float radius)
    {
        // Yarıçapı tam sayıya yuvarlayıp önbellekliyoruz; sürükleme sırasında
        // ondalık oynamalar yüzünden önbellek şişmesin.
        var key = (kind, (int)MathF.Round(radius));
        if (_paths.TryGetValue(key, out var cached))
        {
            return cached;
        }

        var r = key.Item2;
        var path = kind switch
        {
            ShapeKind.Circle => CirclePath(r),
            ShapeKind.Square => SquarePath(r),
            ShapeKind.Triangle => PolygonPath(r, sides: 3, rotation: -MathF.PI / 2f),
            ShapeKind.Hexagon => PolygonPath(r, sides: 6, rotation: -MathF.PI / 2f),
            ShapeKind.Star => StarPath(r),
            ShapeKind.Heart => HeartPath(r),
            _ => CirclePath(r),
        };

        _paths[key] = path;
        return path;
    }

    private static SKPath CirclePath(float r)
    {
        var path = new SKPath();
        path.AddCircle(0, 0, r);
        return path;
    }

    private static SKPath SquarePath(float r)
    {
        // Kenarı köşegene göre ölçekle: kare de daire kadar yer kaplasın.
        var half = r * 0.82f;
        var path = new SKPath();
        path.AddRoundRect(new SKRect(-half, -half, half, half), r * 0.18f, r * 0.18f);
        return path;
    }

    private static SKPath PolygonPath(float r, int sides, float rotation)
    {
        var path = new SKPath();
        for (var i = 0; i < sides; i++)
        {
            var angle = rotation + (MathF.Tau * i / sides);
            var point = new SKPoint(MathF.Cos(angle) * r, MathF.Sin(angle) * r);

            if (i == 0)
            {
                path.MoveTo(point);
            }
            else
            {
                path.LineTo(point);
            }
        }

        path.Close();
        return path;
    }

    private static SKPath StarPath(float r)
    {
        var path = new SKPath();
        var inner = r * 0.46f;

        for (var i = 0; i < 10; i++)
        {
            var angle = (-MathF.PI / 2f) + (MathF.PI * i / 5f);
            var length = i % 2 == 0 ? r : inner;
            var point = new SKPoint(MathF.Cos(angle) * length, MathF.Sin(angle) * length);

            if (i == 0)
            {
                path.MoveTo(point);
            }
            else
            {
                path.LineTo(point);
            }
        }

        path.Close();
        return path;
    }

    private static SKPath HeartPath(float r)
    {
        var path = new SKPath();

        // Alt uçtan başlayıp iki yay ile yukarı çıkıyor.
        path.MoveTo(0, r * 0.85f);
        path.CubicTo(-r * 1.25f, r * 0.05f, -r * 0.62f, -r * 1.0f, 0, -r * 0.34f);
        path.CubicTo(r * 0.62f, -r * 1.0f, r * 1.25f, r * 0.05f, 0, r * 0.85f);
        path.Close();

        return path;
    }

    public void Dispose()
    {
        foreach (var path in _paths.Values)
        {
            path.Dispose();
        }

        _paths.Clear();

        _fill.Shader?.Dispose();
        _shadow.ImageFilter?.Dispose();
        _rim.PathEffect?.Dispose();
        _fill.Dispose();
        _rim.Dispose();
        _shadow.Dispose();
        _gloss.Dispose();
    }
}
