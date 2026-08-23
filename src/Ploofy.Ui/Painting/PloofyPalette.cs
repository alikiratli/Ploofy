using Ploofy.Engine.Games;
using SkiaSharp;

namespace Ploofy.Ui.Painting;

/// <summary>Bir rengin degrade üçlüsü: parlak uç, gövde, gölge.</summary>
/// <remarks>
/// Düz renk yerine üçlü tutulmasının sebebi: çocuk oyunlarında hacim hissi
/// (parlayan üst, koyulaşan alt) şeklin "dokunulabilir" görünmesini sağlıyor.
/// Düz daireler ekranda basılı bir resim gibi duruyor, degrade olanlar
/// dokunulacak bir nesne gibi.
/// </remarks>
public sealed record HuePaint(SKColor Light, SKColor Body, SKColor Shade)
{
    public SKColor Glow => Light.WithAlpha(150);
}

/// <summary>
/// Çizim katmanının renk sözlüğü.
/// </summary>
/// <remarks>
/// XAML tarafındaki tema ile aynı aileden ama ayrı: Skia yüzeyleri
/// <see cref="SKColor"/> istiyor ve degradelerin ara durakları XAML kaynak
/// sözlüğünde ifade edilemiyor. Aynı rengin iki yerde tanımlanmaması için
/// oyun yüzeyleri renklerini yalnızca buradan alıyor.
/// </remarks>
public static class PloofyPalette
{
    public static readonly HuePaint Cherry = new(
        new SKColor(0xFF, 0xB4, 0xBC), new SKColor(0xFF, 0x5C, 0x72), new SKColor(0xD1, 0x1F, 0x45));

    public static readonly HuePaint Ocean = new(
        new SKColor(0xAF, 0xE4, 0xFF), new SKColor(0x3F, 0xB5, 0xF5), new SKColor(0x0F, 0x6F, 0xC4));

    public static readonly HuePaint Lime = new(
        new SKColor(0xCB, 0xF5, 0xA8), new SKColor(0x76, 0xD9, 0x4F), new SKColor(0x2E, 0x93, 0x2B));

    public static readonly HuePaint Sunny = new(
        new SKColor(0xFF, 0xEC, 0xA8), new SKColor(0xFF, 0xC7, 0x33), new SKColor(0xE0, 0x86, 0x00));

    public static readonly HuePaint Grape = new(
        new SKColor(0xDE, 0xCC, 0xFF), new SKColor(0xA3, 0x7C, 0xF0), new SKColor(0x63, 0x37, 0xC4));

    public static readonly HuePaint Bubblegum = new(
        new SKColor(0xFF, 0xD2, 0xE6), new SKColor(0xFF, 0x84, 0xBD), new SKColor(0xDB, 0x2E, 0x86));

    public static HuePaint For(BubbleHue hue) => hue switch
    {
        BubbleHue.Cherry => Cherry,
        BubbleHue.Ocean => Ocean,
        BubbleHue.Lime => Lime,
        BubbleHue.Sunny => Sunny,
        BubbleHue.Grape => Grape,
        BubbleHue.Bubblegum => Bubblegum,
        _ => Ocean,
    };

    /// <summary>Kutlama konfetisinin renkleri — paletin tamamı.</summary>
    public static readonly IReadOnlyList<HuePaint> All =
        [Cherry, Ocean, Lime, Sunny, Grape, Bubblegum];

    // Oyun yüzeyinin göğü. Sıcak üstten serin alta: balonların yükseldiği
    // yön aydınlık olunca hareket yukarı doğru okunuyor.
    public static readonly SKColor SkyTop = new(0xFF, 0xF3, 0xDC);
    public static readonly SKColor SkyMiddle = new(0xFF, 0xDC, 0xEC);
    public static readonly SKColor SkyBottom = new(0xD8, 0xEE, 0xFF);

    public static readonly SKColor Ink = new(0x3A, 0x2A, 0x1E);
}
