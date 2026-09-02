namespace Ploofy.Engine.Games;

/// <summary>
/// Yazılabilir bir işaret: sırayla çizilen darbeler ve çizilmeyen işaretler.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="Strokes"/> çocuğun parmakla takip ettiği kısım, sırasıyla.
/// <see cref="Marks"/> ise yalnızca çizilen süs: Ç'nin kuyruğu, İ'nin noktası,
/// Ö'nün iki noktası. Bunları takip ettirmiyoruz çünkü bir noktayı "takip
/// etmek" diye bir şey yok — parmağın bir yeri var, yönü yok.
/// </para>
/// <para>
/// Bütün koordinatlar 0-1 arası ve ekran yönünde: x sağa, <b>y aşağı</b>.
/// Ekran boyutundan bağımsız olması, aynı verinin telefonda da tablette de
/// çalışması demek.
/// </para>
/// </remarks>
public sealed record Glyph(
    string Character,
    IReadOnlyList<IReadOnlyList<PathPoint>> Strokes,
    IReadOnlyList<IReadOnlyList<PathPoint>> Marks);

/// <summary>
/// Büyük harflerin ve rakamların yazım yolları.
/// </summary>
/// <remarks>
/// <para>
/// Darbe sırası öğretilen sıra: yukarıdan aşağı, soldan sağa, gövde önce süs
/// sonra. Sıra keyfi değil — yanlış sırayla yazmayı öğrenen çocuk bunu
/// sonradan zor bırakıyor, o yüzden oyun sırayı dayatıyor.
/// </para>
/// <para>
/// Yalnızca <b>büyük</b> harfler var. Küçük harfler (a, e, g) çok daha
/// kıvrımlı ve okul öncesinde yazmaya büyük harfle başlanıyor; küçük harf
/// tanıma işi zaten Harf Avı'nda, Meşe bandında yapılıyor.
/// </para>
/// <para>
/// Almanca ß burada yok: sözcük başında hiç bulunmuyor, bu yaşta
/// öğretilmiyor ve tek bir darbeyle anlatılabilecek bir biçimi de yok.
/// </para>
/// </remarks>
public static class GlyphShapes
{
    /// <summary>Bir darbenin kaç noktayla örnekleneceği.</summary>
    /// <remarks>
    /// Takip mekaniği noktalar arasını doğru parçalarla dolduruyor; yay ve
    /// eğrilerin köşeli görünmemesi için bu sayının yeterince büyük olması
    /// gerekiyor. Kırk, ekranda kıvrımı düzleştirmeyen en küçük değer.
    /// </remarks>
    private const int Samples = 40;

    // Harf gövdesinin oturduğu kutu. Kenar payı, parmağın ekran kenarında
    // sıkışmaması için.
    private const float Left = 0.26f;
    private const float Right = 0.74f;
    private const float Top = 0.12f;
    private const float Bottom = 0.88f;
    private const float MidX = 0.5f;
    private const float MidY = 0.5f;

    // Rakamlar harflerden dar: bir rakam hiçbir zaman M kadar geniş değil.
    private const float NumLeft = 0.32f;
    private const float NumRight = 0.68f;

    private static readonly Dictionary<string, Glyph> All = Build();

    /// <summary>Yazılabilir bütün işaretler.</summary>
    public static IReadOnlyCollection<string> Characters => All.Keys;

    /// <summary>İşaretin yazım yolu; tanımlı değilse <c>null</c>.</summary>
    public static Glyph? Find(string character) =>
        All.TryGetValue(character, out var glyph) ? glyph : null;

    public static bool Has(string character) => All.ContainsKey(character);

    private static Dictionary<string, Glyph> Build()
    {
        var glyphs = new Dictionary<string, Glyph>(StringComparer.Ordinal);

        void Add(string character, params IReadOnlyList<PathPoint>[] strokes) =>
            glyphs[character] = new Glyph(character, strokes, []);

        // --- Büyük harfler ---

        Add("A",
            Poly((MidX, Top), (Left, Bottom)),
            Poly((MidX, Top), (Right, Bottom)),
            Poly((0.339f, 0.63f), (0.661f, 0.63f)));

        Add("B",
            Poly((Left, Top), (Left, Bottom)),
            Arc(Left, 0.31f, 0.21f, 0.19f, -90f, 90f),
            Arc(Left, 0.69f, 0.23f, 0.19f, -90f, 90f));

        Add("C", Arc(MidX, MidY, 0.24f, 0.38f, -45f, -315f));

        Add("D",
            Poly((Left, Top), (Left, Bottom)),
            Arc(Left, MidY, 0.26f, 0.38f, -90f, 90f));

        Add("E",
            Poly((Left, Top), (Left, Bottom)),
            Poly((Left, Top), (Right, Top)),
            Poly((Left, MidY), (0.68f, MidY)),
            Poly((Left, Bottom), (Right, Bottom)));

        Add("F",
            Poly((Left, Top), (Left, Bottom)),
            Poly((Left, Top), (Right, Top)),
            Poly((Left, MidY), (0.68f, MidY)));

        Add("G",
            Arc(MidX, MidY, 0.24f, 0.38f, -45f, -315f),
            Poly((0.67f, 0.77f), (0.67f, 0.52f), (0.52f, 0.52f)));

        Add("H",
            Poly((Left, Top), (Left, Bottom)),
            Poly((Right, Top), (Right, Bottom)),
            Poly((Left, MidY), (Right, MidY)));

        // Türkçe'nin noktasız büyük I'sı; İngilizce ve Almanca I ile aynı.
        Add("I", Poly((MidX, Top), (MidX, Bottom)));

        Add("J",
            Poly((0.62f, Top), (0.62f, 0.66f)),
            Arc(0.45f, 0.66f, 0.17f, 0.20f, 0f, 180f));

        Add("K",
            Poly((Left, Top), (Left, Bottom)),
            Poly((Right, Top), (Left, 0.55f)),
            Poly((Left, 0.55f), (Right, Bottom)));

        Add("L", Poly((Left, Top), (Left, Bottom), (Right, Bottom)));

        Add("M", Poly((Left, Bottom), (Left, Top), (MidX, 0.58f), (Right, Top), (Right, Bottom)));

        Add("N", Poly((Left, Bottom), (Left, Top), (Right, Bottom), (Right, Top)));

        Add("O", Arc(MidX, MidY, 0.25f, 0.38f, -90f, -450f));

        Add("P",
            Poly((Left, Top), (Left, Bottom)),
            Arc(Left, 0.31f, 0.21f, 0.19f, -90f, 90f));

        Add("Q",
            Arc(MidX, MidY, 0.25f, 0.38f, -90f, -450f),
            Poly((0.60f, 0.68f), (0.76f, 0.90f)));

        Add("R",
            Poly((Left, Top), (Left, Bottom)),
            Arc(Left, 0.31f, 0.21f, 0.19f, -90f, 90f),
            Poly((Left, MidY), (Right, Bottom)));

        Add("S", Curve(
            (0.70f, 0.22f), (0.55f, 0.13f), (0.37f, 0.18f), (0.33f, 0.32f),
            (0.44f, 0.44f), (0.60f, 0.54f), (0.68f, 0.66f), (0.64f, 0.81f),
            (0.47f, 0.88f), (0.31f, 0.79f)));

        Add("T",
            Poly((Left, Top), (Right, Top)),
            Poly((MidX, Top), (MidX, Bottom)));

        Add("U", Curve(
            (Left, Top), (Left, 0.50f), (MidX, 0.88f), (Right, 0.50f), (Right, Top)));

        Add("V", Poly((Left, Top), (MidX, Bottom), (Right, Top)));

        Add("W", Poly(
            (0.22f, Top), (0.35f, Bottom), (MidX, 0.42f), (0.65f, Bottom), (0.78f, Top)));

        Add("X",
            Poly((Left, Top), (Right, Bottom)),
            Poly((Right, Top), (Left, Bottom)));

        Add("Y",
            Poly((Left, Top), (MidX, 0.52f), (MidX, Bottom)),
            Poly((Right, Top), (MidX, 0.52f)));

        Add("Z", Poly((Left, Top), (Right, Top), (Left, Bottom), (Right, Bottom)));

        // --- Rakamlar ---

        Add("0", Arc(MidX, MidY, 0.19f, 0.38f, -90f, -450f));

        Add("1", Poly((0.38f, 0.24f), (0.50f, Top), (0.50f, Bottom)));

        Add("2", Curve(
            (NumLeft, 0.26f), (0.42f, 0.13f), (0.60f, 0.16f), (0.66f, 0.31f),
            (0.54f, 0.49f), (0.34f, 0.72f), (0.33f, Bottom), (NumRight, Bottom)));

        Add("3",
            Curve((0.34f, 0.22f), (0.46f, Top), (0.64f, 0.19f), (0.62f, 0.35f), (0.48f, 0.45f)),
            Curve((0.48f, 0.45f), (0.68f, 0.53f), (0.67f, 0.75f), (0.50f, Bottom), (0.34f, 0.80f)));

        Add("4",
            Poly((0.60f, Top), (0.30f, 0.62f), (0.72f, 0.62f)),
            Poly((0.60f, Top), (0.60f, Bottom)));

        Add("5",
            Poly((0.66f, 0.14f), (0.38f, 0.14f), (0.35f, 0.42f)),
            Curve((0.35f, 0.42f), (0.55f, 0.37f), (0.68f, 0.53f), (0.64f, 0.75f),
                  (0.44f, Bottom), (0.32f, 0.80f)));

        Add("6", Curve(
            (0.62f, 0.16f), (0.44f, 0.23f), (0.34f, 0.45f), (0.34f, 0.68f),
            (0.48f, 0.86f), (0.63f, 0.74f), (0.59f, 0.57f), (0.42f, 0.53f), (0.35f, 0.62f)));

        Add("7", Poly((0.32f, 0.14f), (0.70f, 0.14f), (0.44f, Bottom)));

        // Sekiz iki halka: tek darbede yazılan sekiz kendini kesiyor ve
        // kesişme noktasında parmağın hangi kolda olduğu belirsizleşiyor.
        Add("8",
            Arc(MidX, 0.31f, 0.15f, 0.19f, -90f, -450f),
            Arc(MidX, 0.66f, 0.18f, 0.22f, -90f, -450f));

        Add("9",
            Arc(MidX, 0.32f, 0.17f, 0.20f, -90f, -450f),
            Poly((0.67f, 0.32f), (0.62f, Bottom)));

        // --- Aksanlı harfler: gövde aynı, süs çizilmiyor ---

        Decorate(glyphs, "Ç", "C", Cedilla(MidX, 0.88f));
        Decorate(glyphs, "Ş", "S", Cedilla(0.47f, 0.88f));
        Decorate(glyphs, "Ğ", "G", Breve(MidX, 0.10f));
        Decorate(glyphs, "İ", "I", Dot(MidX, 0.06f));
        Decorate(glyphs, "Ö", "O", Dot(0.42f, 0.05f), Dot(0.58f, 0.05f));
        Decorate(glyphs, "Ü", "U", Dot(0.42f, 0.05f), Dot(0.58f, 0.05f));
        Decorate(glyphs, "Ä", "A", Dot(0.42f, 0.05f), Dot(0.58f, 0.05f));

        return glyphs;
    }

    /// <summary>
    /// Aksanlı harfi taban harfin gövdesinden türetir.
    /// </summary>
    /// <remarks>
    /// Ç ile C'nin yazımı arasındaki tek fark kuyruk, ve kuyruk takip
    /// edilmiyor. Gövdeyi kopyalamak yerine paylaşmak, C düzeltilince Ç'nin
    /// de düzelmesi demek.
    /// </remarks>
    private static void Decorate(
        Dictionary<string, Glyph> glyphs,
        string character,
        string baseCharacter,
        params IReadOnlyList<PathPoint>[] marks)
    {
        var body = glyphs[baseCharacter];
        glyphs[character] = new Glyph(character, body.Strokes, marks);
    }

    /// <summary>Nokta: küçük bir çember. Takip edilmiyor, yalnızca çiziliyor.</summary>
    private static IReadOnlyList<PathPoint> Dot(float x, float y) =>
        Arc(x, y, 0.022f, 0.022f, -90f, -450f);

    /// <summary>Ç ve Ş'nin kuyruğu.</summary>
    private static IReadOnlyList<PathPoint> Cedilla(float x, float y) =>
        Curve((x, y), (x + 0.02f, y + 0.05f), (x - 0.03f, y + 0.07f));

    /// <summary>Ğ'nin üstündeki kaşık.</summary>
    private static IReadOnlyList<PathPoint> Breve(float x, float y) =>
        Arc(x, y - 0.02f, 0.08f, 0.05f, 20f, 160f);

    /// <summary>Köşeli çizgi dizisi, boy boyunca eşit örneklenir.</summary>
    private static IReadOnlyList<PathPoint> Poly(params (float X, float Y)[] corners)
    {
        var points = corners.Select(c => new PathPoint(c.X, c.Y)).ToList();
        return Resample(points);
    }

    /// <summary>
    /// Elips yayı.
    /// </summary>
    /// <remarks>
    /// Açı derece; y aşağı olduğu için artan açı ekranda <b>saat yönünde</b>
    /// dönüyor. Sıfır derece sağ uç, -90 üst uç. Bitiş açısı başlangıçtan
    /// küçükse yay saat yönünün tersine çiziliyor — C ve O'nun yazım yönü bu.
    /// </remarks>
    private static IReadOnlyList<PathPoint> Arc(
        float cx, float cy, float rx, float ry, float fromDegrees, float toDegrees)
    {
        var points = new List<PathPoint>(Samples);
        for (var i = 0; i < Samples; i++)
        {
            var t = i / (float)(Samples - 1);
            var angle = (fromDegrees + ((toDegrees - fromDegrees) * t)) * MathF.PI / 180f;

            points.Add(new PathPoint(
                cx + (rx * MathF.Cos(angle)),
                cy + (ry * MathF.Sin(angle))));
        }

        return points;
    }

    /// <summary>
    /// Yumuşak eğri: verilen noktalardan geçen Catmull-Rom.
    /// </summary>
    /// <remarks>
    /// Elle yazılan noktalar eğrinin <b>üstünde</b> duruyor (Bezier'de olduğu
    /// gibi dışında değil), bu yüzden S ya da 6 gibi bir biçimi ayarlamak
    /// noktayı gözle doğru yere koymaktan ibaret.
    /// </remarks>
    private static IReadOnlyList<PathPoint> Curve(params (float X, float Y)[] controls)
    {
        var pts = controls.Select(c => new PathPoint(c.X, c.Y)).ToList();

        // Uçlarda komşu eksik; ilk ve son nokta ikişer kez sayılıyor.
        var padded = new List<PathPoint> { pts[0] };
        padded.AddRange(pts);
        padded.Add(pts[^1]);

        var dense = new List<PathPoint>();
        const int PerSegment = 12;

        for (var i = 0; i < padded.Count - 3; i++)
        {
            var p0 = padded[i];
            var p1 = padded[i + 1];
            var p2 = padded[i + 2];
            var p3 = padded[i + 3];

            for (var step = 0; step < PerSegment; step++)
            {
                var t = step / (float)PerSegment;
                dense.Add(CatmullRom(p0, p1, p2, p3, t));
            }
        }

        dense.Add(pts[^1]);
        return Resample(dense);
    }

    private static PathPoint CatmullRom(PathPoint p0, PathPoint p1, PathPoint p2, PathPoint p3, float t)
    {
        var t2 = t * t;
        var t3 = t2 * t;

        float Axis(float a, float b, float c, float d) =>
            0.5f * ((2f * b)
                + ((c - a) * t)
                + (((2f * a) - (5f * b) + (4f * c) - d) * t2)
                + ((-a + (3f * b) - (3f * c) + d) * t3));

        return new PathPoint(Axis(p0.X, p1.X, p2.X, p3.X), Axis(p0.Y, p1.Y, p2.Y, p3.Y));
    }

    /// <summary>
    /// Yolu boy boyunca eşit aralıklı <see cref="Samples"/> noktaya indirger.
    /// </summary>
    /// <remarks>
    /// Eşit aralık şart: takip mekaniği ilerlemeyi nokta indisinden sayıyor.
    /// Noktalar bir yerde sık bir yerde seyrek olursa, sık bölge yolun
    /// gereğinden büyük bir parçası sayılıyor ve çocuk L'nin dikeyini
    /// bitirdiğinde harf yarılanmış görünüyor.
    /// </remarks>
    private static IReadOnlyList<PathPoint> Resample(IReadOnlyList<PathPoint> source)
    {
        var lengths = new float[source.Count];
        var total = 0f;

        for (var i = 1; i < source.Count; i++)
        {
            total += TracePath.Distance(source[i].X, source[i].Y, source[i - 1].X, source[i - 1].Y);
            lengths[i] = total;
        }

        if (total <= float.Epsilon)
        {
            return [source[0], source[^1]];
        }

        var result = new List<PathPoint>(Samples);
        var cursor = 1;

        for (var i = 0; i < Samples; i++)
        {
            var target = total * i / (Samples - 1);

            while (cursor < source.Count - 1 && lengths[cursor] < target)
            {
                cursor++;
            }

            var span = lengths[cursor] - lengths[cursor - 1];
            var t = span <= float.Epsilon ? 0f : (target - lengths[cursor - 1]) / span;

            var from = source[cursor - 1];
            var to = source[cursor];

            result.Add(new PathPoint(
                from.X + ((to.X - from.X) * t),
                from.Y + ((to.Y - from.Y) * t)));
        }

        return result;
    }
}
