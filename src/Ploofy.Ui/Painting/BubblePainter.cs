using SkiaSharp;

namespace Ploofy.Ui.Painting;

/// <summary>
/// Cam gibi parlayan bir balon çizer.
/// </summary>
/// <remarks>
/// <para>
/// Balon dört katmandan oluşuyor ve her katmanın bir işi var:
/// </para>
/// <list type="number">
///   <item>Zemine düşen yumuşak gölge — balonu ekrandan koparıp öne getiriyor.</item>
///   <item>Gövde degradesi — üstten aydınlık, alttan koyu; hacim hissi bundan.</item>
///   <item>İnce kenar halkası — balonu arka plandan ayırıyor, açık zeminde
///         kaybolmasını önlüyor.</item>
///   <item>Parlama noktaları — büyük bir ışık lekesi ve küçük bir kıvılcım;
///         "cam" hissini veren tek şey bu ikisi.</item>
/// </list>
/// <para>
/// Boya nesneleri her karede değil bir kez üretiliyor: 60 kare/saniye çizimde
/// <see cref="SKPaint"/> ayırmak, hedeflenen düşük donanımlı tabletlerde
/// gözle görülür takılma yapıyor.
/// </para>
/// </remarks>
public sealed class BubblePainter : IDisposable
{
    private readonly SKPaint _fill = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _rim = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };
    private readonly SKPaint _shadow = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _gloss = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    /// <summary>
    /// Balonu çizer.
    /// </summary>
    /// <param name="scale">
    /// 1 normal boy. Doğarken büyüyen, patlarken şişen balon için çizim
    /// katmanı bunu değiştiriyor.
    /// </param>
    /// <param name="squash">
    /// Nefes alma esnemesi (0 = daire). Balonların hepsi aynı anda esnemesin
    /// diye her balonun kendi faz kayması var.
    /// </param>
    public void Draw(
        SKCanvas canvas,
        SKPoint center,
        float radius,
        HuePaint hue,
        float scale = 1f,
        float squash = 0f,
        byte alpha = 255)
    {
        if (radius <= 0f || scale <= 0f)
        {
            return;
        }

        var r = radius * scale;
        var rx = r * (1f + squash);
        var ry = r * (1f - squash);

        // 1) Gölge — balonun biraz altında ve daha yumuşak.
        _shadow.Color = new SKColor(0x2A, 0x1A, 0x3A, (byte)(alpha * 0.18f));
        _shadow.ImageFilter?.Dispose();
        _shadow.ImageFilter = SKImageFilter.CreateBlur(r * 0.22f, r * 0.22f);
        canvas.DrawOval(center.X, center.Y + (r * 0.14f), rx * 0.92f, ry * 0.92f, _shadow);

        // 2) Gövde — ışık kaynağı sol üstte olduğu için degradenin merkezi de orada.
        var lightCenter = new SKPoint(center.X - (rx * 0.3f), center.Y - (ry * 0.34f));
        _fill.Shader?.Dispose();
        _fill.Shader = SKShader.CreateRadialGradient(
            lightCenter,
            r * 1.45f,
            [hue.Light.WithAlpha(alpha), hue.Body.WithAlpha(alpha), hue.Shade.WithAlpha(alpha)],
            [0f, 0.55f, 1f],
            SKShaderTileMode.Clamp);
        canvas.DrawOval(center.X, center.Y, rx, ry, _fill);

        // 3) Kenar halkası.
        _rim.StrokeWidth = MathF.Max(1.5f, r * 0.06f);
        _rim.Color = hue.Light.WithAlpha((byte)(alpha * 0.75f));
        canvas.DrawOval(center.X, center.Y, rx * 0.985f, ry * 0.985f, _rim);

        // 4) Parlamalar.
        _gloss.Shader?.Dispose();
        _gloss.Shader = null;
        _gloss.Color = SKColors.White.WithAlpha((byte)(alpha * 0.55f));
        canvas.Save();
        canvas.Translate(center.X - (rx * 0.34f), center.Y - (ry * 0.38f));
        canvas.RotateDegrees(-28f);
        canvas.DrawOval(0, 0, rx * 0.3f, ry * 0.18f, _gloss);
        canvas.Restore();

        _gloss.Color = SKColors.White.WithAlpha((byte)(alpha * 0.8f));
        canvas.DrawCircle(
            center.X + (rx * 0.36f),
            center.Y + (ry * 0.34f),
            r * 0.07f,
            _gloss);
    }

    public void Dispose()
    {
        _fill.Shader?.Dispose();
        _shadow.ImageFilter?.Dispose();
        _gloss.Shader?.Dispose();
        _fill.Dispose();
        _rim.Dispose();
        _shadow.Dispose();
        _gloss.Dispose();
    }
}
