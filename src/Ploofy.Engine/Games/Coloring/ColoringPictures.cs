namespace Ploofy.Engine.Games;

/// <summary>Boyanacak bir alanın köşesi. 0-1 arası; x sağa, <b>y aşağı</b>.</summary>
public readonly record struct ColorPoint(float X, float Y);

/// <summary>
/// Boyanabilir tek alan.
/// </summary>
/// <param name="Id">Resim içinde benzersiz anahtar; dolgu kaydı buna bağlı.</param>
/// <param name="Outline">
/// Kapalı çokgen, köşe sırasıyla. Son köşe birinciye kendiliğinden bağlanıyor.
/// </param>
public sealed record ColoringRegion(string Id, IReadOnlyList<ColorPoint> Outline)
{
    /// <summary>
    /// Nokta bu alanın içinde mi?
    /// </summary>
    /// <remarks>
    /// Işın atma (ray casting): noktadan sağa doğru sonsuz bir ışın çizilip
    /// kenarlarla kaç kez kesiştiğine bakılıyor. Tek sayıysa içeride. Çokgen
    /// dışbükey olmak zorunda değil — çiçeğin yaprağı da, evin çatısı da bu
    /// yöntemle doğru cevap veriyor.
    /// </remarks>
    public bool Contains(float x, float y)
    {
        var inside = false;

        for (int i = 0, j = Outline.Count - 1; i < Outline.Count; j = i++)
        {
            var a = Outline[i];
            var b = Outline[j];

            // Kenar, noktanın y'sini kesiyor mu? Bir uç üstte bir uç altta
            // olmalı; eşitlik yalnızca bir tarafta sayılıyor, yoksa tam
            // köşeye denk gelen dokunuş iki kez sayılıyor.
            if (a.Y > y != b.Y > y)
            {
                var crossX = ((b.X - a.X) * (y - a.Y) / (b.Y - a.Y)) + a.X;
                if (x < crossX)
                {
                    inside = !inside;
                }
            }
        }

        return inside;
    }
}

/// <summary>Boyama sayfası: sırayla çizilen alanlar.</summary>
/// <param name="Regions">
/// <b>Arkadan öne</b> sıralı. Ekran bu sırayla çiziyor, dokunma ise ters
/// sırayla arıyor — üstteki alan altındakini gölgeliyor. Evin kapısı
/// duvarın üstünde duruyor ve duvara dokunmak kapıyı boyamıyor.
/// </param>
public sealed record ColoringPicture(string Id, IReadOnlyList<ColoringRegion> Regions)
{
    public int RegionCount => Regions.Count;

    /// <summary>Noktanın düştüğü en üstteki alan; hiçbiri değilse null.</summary>
    public ColoringRegion? HitTest(float x, float y)
    {
        for (var i = Regions.Count - 1; i >= 0; i--)
        {
            if (Regions[i].Contains(x, y))
            {
                return Regions[i];
            }
        }

        return null;
    }
}

/// <summary>
/// Boyama sayfaları.
/// </summary>
/// <remarks>
/// <para>
/// Alanlar çokgen, çünkü boyamanın tamamı bir <b>nokta içeride mi</b>
/// sorusuna iniyor ve çokgen bu soruyu emoji ya da hazır görsel olmadan,
/// motorun içinde cevaplayabiliyor. Yuvarlak biçimler
/// <see cref="Circle"/> ile örnekleniyor: yeterince köşeli bir çember
/// ekranda çember görünüyor.
/// </para>
/// <para>
/// Sıra <b>arkadan öne</b>: gövde önce, üstündeki süs sonra. Sıra yanlış
/// olursa kapı duvarın altında kalıyor ve ona dokunulamıyor.
/// </para>
/// </remarks>
public static class ColoringPictures
{
    /// <summary>Bir çemberin kaç köşeyle örnekleneceği.</summary>
    /// <remarks>
    /// Yirmi dört, ekranda köşeliliği görünmeyen en küçük değer. Daha
    /// fazlası dokunma testini gereksiz yere ağırlaştırıyor — her dokunuşta
    /// bütün kenarlar geziliyor.
    /// </remarks>
    private const int CircleSamples = 24;

    public static readonly IReadOnlyList<ColoringPicture> All =
    [
        // Ev: duvar, çatı, kapı, pencere. Kütüphanenin en basit sayfası ve
        // genelde çocuğun ilk boyadığı.
        new("house",
        [
            Region("wall", (0.22f, 0.46f), (0.78f, 0.46f), (0.78f, 0.86f), (0.22f, 0.86f)),
            Region("roof", (0.16f, 0.46f), (0.50f, 0.16f), (0.84f, 0.46f)),
            Region("door", (0.44f, 0.62f), (0.60f, 0.62f), (0.60f, 0.86f), (0.44f, 0.86f)),
            Region("window", (0.28f, 0.54f), (0.40f, 0.54f), (0.40f, 0.66f), (0.28f, 0.66f)),
        ]),

        // Balık: gövde, kuyruk, sırt yüzgeci, göz.
        new("fish",
        [
            Region("body",
                (0.86f, 0.50f), (0.72f, 0.30f), (0.46f, 0.26f),
                (0.26f, 0.40f), (0.26f, 0.60f), (0.46f, 0.74f), (0.72f, 0.70f)),
            Region("tail", (0.28f, 0.50f), (0.08f, 0.28f), (0.08f, 0.72f)),
            Region("fin", (0.48f, 0.28f), (0.58f, 0.10f), (0.66f, 0.30f)),
            Circle("eye", 0.74f, 0.44f, 0.045f),
        ]),

        // Çiçek: sap, iki yaprak, beş taç yaprağı, orta. On alan —
        // kütüphanenin en zengin sayfası, Filiz'e verilmiyor.
        new("flower",
        [
            Region("stem", (0.47f, 0.52f), (0.53f, 0.52f), (0.53f, 0.92f), (0.47f, 0.92f)),
            Region("leafLeft", (0.47f, 0.70f), (0.24f, 0.64f), (0.26f, 0.78f)),
            Region("leafRight", (0.53f, 0.76f), (0.76f, 0.70f), (0.74f, 0.84f)),
            Circle("petalTop", 0.50f, 0.16f, 0.11f),
            Circle("petalRight", 0.66f, 0.27f, 0.11f),
            Circle("petalLowerRight", 0.60f, 0.45f, 0.11f),
            Circle("petalLowerLeft", 0.40f, 0.45f, 0.11f),
            Circle("petalLeft", 0.34f, 0.27f, 0.11f),
            Circle("center", 0.50f, 0.31f, 0.10f),
        ]),

        // Araba: gövde, üst kabin, iki tekerlek.
        new("car",
        [
            Region("body", (0.10f, 0.52f), (0.90f, 0.52f), (0.90f, 0.72f), (0.10f, 0.72f)),
            Region("cabin", (0.30f, 0.52f), (0.38f, 0.32f), (0.66f, 0.32f), (0.72f, 0.52f)),
            Circle("wheelFront", 0.72f, 0.74f, 0.10f),
            Circle("wheelRear", 0.28f, 0.74f, 0.10f),
        ]),

        // Kelebek: gövde ve dört kanat.
        new("butterfly",
        [
            Region("body", (0.47f, 0.22f), (0.53f, 0.22f), (0.53f, 0.80f), (0.47f, 0.80f)),
            Region("wingUpperLeft", (0.47f, 0.30f), (0.24f, 0.12f), (0.10f, 0.34f), (0.44f, 0.48f)),
            Region("wingUpperRight", (0.53f, 0.30f), (0.76f, 0.12f), (0.90f, 0.34f), (0.56f, 0.48f)),
            Region("wingLowerLeft", (0.46f, 0.52f), (0.16f, 0.60f), (0.28f, 0.84f), (0.47f, 0.72f)),
            Region("wingLowerRight", (0.54f, 0.52f), (0.84f, 0.60f), (0.72f, 0.84f), (0.53f, 0.72f)),
        ]),
    ];

    /// <summary>Alan sayısı verilen sınırın altında kalan sayfalar.</summary>
    public static IReadOnlyList<ColoringPicture> UpTo(int maxRegions) =>
        [.. All.Where(p => p.RegionCount <= maxRegions)];

    public static ColoringPicture? Find(string id) => All.FirstOrDefault(p => p.Id == id);

    private static ColoringRegion Region(string id, params (float X, float Y)[] points) =>
        new(id, [.. points.Select(p => new ColorPoint(p.X, p.Y))]);

    private static ColoringRegion Circle(string id, float cx, float cy, float radius)
    {
        var points = new List<ColorPoint>(CircleSamples);

        for (var i = 0; i < CircleSamples; i++)
        {
            var angle = MathF.Tau * i / CircleSamples;
            points.Add(new ColorPoint(
                cx + (radius * MathF.Cos(angle)),
                cy + (radius * MathF.Sin(angle))));
        }

        return new ColoringRegion(id, points);
    }
}
