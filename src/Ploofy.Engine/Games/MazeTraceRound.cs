using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Yolun biçimi.</summary>
/// <remarks>
/// Dördü de aynı iskeletten türüyor: iki uç arasında bir taşıyıcı eksen ve o
/// eksene <b>dik</b> bir sapma. Biçimi belirleyen sapma fonksiyonu. Bu yüzden
/// yollar kendi üstünden geçmiyor — ve geçmemeli: kesişen bir yolda hem
/// ilerlemenin hangi kolda olduğu belirsizleşiyor hem de çocuk "hangi yoldan
/// gideceğim" diye takılıyor.
/// </remarks>
public enum PathShape
{
    /// <summary>Düz çizgi.</summary>
    Straight,

    /// <summary>Tek bir yumuşak kambur.</summary>
    Arc,

    /// <summary>Yumuşak inip çıkan dalga.</summary>
    Wave,

    /// <summary>Keskin köşeli zikzak.</summary>
    Zigzag,
}

/// <summary>Parmağın yaptığı şeyin sonucu.</summary>
public enum TraceOutcome
{
    /// <summary>Yok sayıldı: yol tutulmuş değil, dokunuş yolun dışında, tur bitmiş.</summary>
    Ignored,

    /// <summary>Parmak yola kondu, takip başladı.</summary>
    Started,

    /// <summary>Yol boyunca ilerlendi.</summary>
    Advanced,

    /// <summary>Yoldan çıkıldı.</summary>
    Slipped,

    /// <summary>Yolun sonuna ulaşıldı.</summary>
    LevelComplete,
}

/// <summary>Yol üstündeki tek nokta.</summary>
/// <remarks>
/// Koordinatlar <b>birim kare</b> içinde (0-1 hem yatayda hem dikeyde).
/// Balon Patlatma'daki gibi "X genişliğe, Y yüksekliğe göre" değil: bu oyunda
/// "yoldan çıkmak" her yönde aynı mesafe olmak zorunda ve ekran kare değil.
/// Arayüz bu kareyi ekranın ortasına oturtuyor.
/// </remarks>
public readonly record struct PathPoint(float X, float Y);

/// <summary>Yolu Bul'un banda göre zorluk tablosu.</summary>
public static class MazeTraceTuning
{
    /// <summary>Turu bitirmek için tamamlanması gereken yol sayısı.</summary>
    public static readonly BandValue<int> Levels = new(3, 4, 5);

    /// <summary>
    /// Yoldan sayılan en büyük sapma (birim karenin kenarına oranla).
    /// </summary>
    /// <remarks>
    /// Zorluğun yarısı burada. Filiz'de yol kalın bir şerit gibi davranıyor;
    /// Meşe'de ince bir çizgi. Bunun altına inmek anlamsız — parmak ucu
    /// tablette zaten bu kadar yer kaplıyor.
    /// </remarks>
    public static readonly BandValue<float> Tolerance = new(0.11f, 0.08f, 0.055f);

    /// <summary>Hangi biçimler çıkabilir.</summary>
    /// <remarks>
    /// Meşe'de düz çizgi yok: o bantta düz bir yolu takip etmek beceri değil.
    /// Keskin köşe (zikzak) asıl zor olan — köşede durup yön değiştirmek
    /// gerekiyor, oysa yumuşak eğri parmağı kendi taşıyor.
    /// </remarks>
    public static readonly BandValue<PathShape[]> Shapes = new(
        [PathShape.Straight, PathShape.Arc],
        [PathShape.Straight, PathShape.Arc, PathShape.Wave, PathShape.Zigzag],
        [PathShape.Arc, PathShape.Wave, PathShape.Zigzag, PathShape.Zigzag]);

    /// <summary>
    /// Biçimin ne kadar kıvrımlı olacağı: dalga sayısı ve zikzak köşe sayısı
    /// bundan türüyor.
    /// </summary>
    public static readonly BandValue<float> Complexity = new(1f, 1.8f, 2.6f);

    /// <summary>Yoldan çıkmak hata sayılıyor mu?</summary>
    public static readonly BandValue<bool> CountsSlips = new(false, false, true);

    // Hedef süre yok. Yolu hızlı çizmek daha iyi çizmek değil; tersine bu
    // yaşta acele, tam olarak engellemeye çalıştığımız şeyi — köşeyi kesip
    // geçmeyi — ödüllendirirdi. Meşe'nin üçüncü yıldızı yoldan çıkmamaya
    // bağlı. Aynı gerekçe Sırayı Tekrarla ve Sepeti Tut'ta da geçerli.
}

/// <summary>
/// Yolu Bul oyununun bir turu — çizimden bağımsız kurallar.
/// </summary>
/// <remarks>
/// <para>
/// Ekranda bir yol duruyor; çocuk parmağını başlangıca koyup yolu takip
/// ederek sona götürüyor. Kütüphanenin tek <see cref="Catalog.InteractionKind.Trace"/>
/// oyunu ve yazı öncesi becerinin doğrudan karşılığı: çizgi, eğri ve köşe
/// takip etmek kalem tutmanın hazırlığı.
/// </para>
/// <para>
/// İlerleme <b>geri gitmiyor</b>. Parmak geriye kayarsa ilerleme olduğu
/// yerde kalıyor, çocuk yeniden ileri gidince kaldığı yerden devam ediyor.
/// Geri saymak, titreyen bir parmağın oyunu bitirememesi demek olurdu.
/// </para>
/// <para>
/// Parmak kalkarsa ilerleme <b>korunuyor</b>; kaldığı yere yeniden dokunup
/// devam edebiliyor. Bu yüzden arayüzün <see cref="Head"/> noktasını açıkça
/// göstermesi şart — çocuk parmağını nereye koyacağını başka türlü bilemez.
/// </para>
/// </remarks>
public sealed class MazeTraceRound
{
    /// <summary>Yol kaç noktayla örnekleniyor.</summary>
    private const int Samples = 120;

    /// <summary>
    /// İlerlemenin bir hamlede atlayabileceği en fazla parça sayısı.
    /// </summary>
    /// <remarks>
    /// Parmağın yola en yakın noktası yalnızca bu pencere içinde aranıyor.
    /// Penceresiz arama, yola yakın geçen ileri bir bölüme atlamayı mümkün
    /// kılıyor: çocuk parmağını kaldırıp yolun ortasına koyarak yarısını
    /// atlayabiliyordu.
    /// </remarks>
    private const int Lookahead = 10;

    /// <summary>Yolun sonu sayılan ilerleme oranı.</summary>
    /// <remarks>
    /// Son noktaya birebir değmesini beklemiyoruz: yolun sonuna gelmiş bir
    /// çocuğun son birkaç pikseli de kovalaması, bitirmeyi bir beceriden
    /// inada çeviriyor.
    /// </remarks>
    private const float FinishAt = 0.96f;

    private readonly Random _rng;
    private readonly List<PathPoint> _points = new(Samples);

    private float _progressIndex;
    private bool _isOffPath;

    private MazeTraceRound(AgeBand band, Random rng)
    {
        Band = band;
        _rng = rng;

        Total = MazeTraceTuning.Levels.For(band);
        Tolerance = MazeTraceTuning.Tolerance.For(band);
        Complexity = MazeTraceTuning.Complexity.For(band);
        CountsSlips = MazeTraceTuning.CountsSlips.For(band);

        BuildPath();
    }

    public static MazeTraceRound ForBand(AgeBand band, Random? random = null) =>
        new(band, random ?? Random.Shared);

    public AgeBand Band { get; }

    /// <summary>Tamamlanması gereken yol sayısı.</summary>
    public int Total { get; }

    public int Completed { get; private set; }

    /// <summary>Yoldan çıkma sayısı — hata sayılıp sayılmadığından bağımsız.</summary>
    public int Slips { get; private set; }

    /// <summary>Yıldız hesabına giden hata sayısı.</summary>
    public int Mistakes => CountsSlips ? Slips : 0;

    public bool CountsSlips { get; }

    /// <summary>Yoldan sayılan en büyük sapma.</summary>
    public float Tolerance { get; }

    public float Complexity { get; }

    /// <summary>Parmağın konabileceği alan, <see cref="Tolerance"/>'tan geniş.</summary>
    /// <remarks>
    /// Parmağı ince bir çizginin tam üstüne indirmek, onu takip etmekten
    /// daha zor: dokunmadan önce parmağın altını göremiyorsun.
    /// </remarks>
    public float GrabTolerance => Tolerance * 1.7f;

    /// <summary>Şu anki yolun biçimi.</summary>
    public PathShape Shape { get; private set; }

    /// <summary>Yolun örneklenmiş noktaları, baştan sona.</summary>
    public IReadOnlyList<PathPoint> Points => _points;

    public PathPoint Start => _points[0];

    public PathPoint Goal => _points[^1];

    /// <summary>Parmak yolu tutuyor mu?</summary>
    public bool IsTracing { get; private set; }

    /// <summary>Parmak şu an yolun dışında mı?</summary>
    public bool IsOffPath => IsTracing && _isOffPath;

    /// <summary>Yolun ne kadarı geçildi (0-1).</summary>
    public float Progress => _progressIndex / (_points.Count - 1);

    /// <summary>İlerlemenin ucu — arayüz parmağın nereye konacağını buradan gösteriyor.</summary>
    public PathPoint Head => PointAt(_progressIndex);

    public bool IsComplete => Completed >= Total;

    /// <summary>
    /// Parmağı yola koyar.
    /// </summary>
    /// <remarks>
    /// Kabul edilen tek yer <see cref="Head"/> çevresi — yolun başı değil.
    /// Yarısına kadar gelmiş bir çocuk başlangıca dokunursa hiçbir şey
    /// olmuyor, çünkü ilerleme geri gitmiyor ve oradan devam etmek yolun
    /// yarısını boşuna çizmek olurdu.
    /// </remarks>
    public TraceOutcome Begin(float x, float y)
    {
        if (IsComplete)
        {
            return TraceOutcome.Ignored;
        }

        var head = Head;
        if (Distance(x, y, head.X, head.Y) > GrabTolerance)
        {
            return TraceOutcome.Ignored;
        }

        IsTracing = true;
        _isOffPath = false;
        return TraceOutcome.Started;
    }

    /// <summary>Parmağı yol boyunca taşır.</summary>
    public TraceOutcome MoveTo(float x, float y)
    {
        if (!IsTracing || IsComplete)
        {
            return TraceOutcome.Ignored;
        }

        var (index, distance) = NearestAhead(x, y);

        if (distance > Tolerance)
        {
            // Bir çıkış bir hata. Her karede saymak, yolun dışında duran bir
            // parmağı saniyede altmış hataya çeviriyordu.
            if (!_isOffPath)
            {
                _isOffPath = true;
                Slips++;
            }

            return TraceOutcome.Slipped;
        }

        _isOffPath = false;

        // İlerleme geri gitmiyor: titreyen parmak kazanılanı geri almıyor.
        if (index > _progressIndex)
        {
            _progressIndex = index;
        }

        if (Progress < FinishAt)
        {
            return TraceOutcome.Advanced;
        }

        _progressIndex = _points.Count - 1;
        Completed++;
        IsTracing = false;

        if (!IsComplete)
        {
            BuildPath();
        }

        return TraceOutcome.LevelComplete;
    }

    /// <summary>
    /// Parmağı kaldırır.
    /// </summary>
    /// <remarks>
    /// Hata değil ve ilerleme silinmiyor. Küçük çocuk parmağını uzun süre
    /// ekranda tutamıyor; kalkmayı cezalandırmak oyunu bitirilemez yapardı.
    /// </remarks>
    public void Release()
    {
        IsTracing = false;
        _isOffPath = false;
    }

    /// <summary>
    /// Parmağın yola en yakın olduğu yer — ileriye doğru pencere içinde.
    /// </summary>
    /// <remarks>
    /// Pencere yalnızca <b>ileriyi</b> sınırlıyor; geriye doğru yolun tamamı
    /// açık. Sebebi cihazda görüldü: dar bir geri pay (iki parça, yaklaşık
    /// yolun %1,7'si) bırakıldığında köşede geriye kayan parmak yoldan
    /// çıkmış sayılıyordu — Meşe'de bu bir hata puanı, ve o kadar geri
    /// kayma beş yaşındaki bir çocuğun titremesi kadar bir mesafe.
    /// Geriye gitmek zaten hiçbir şey kazandırmıyor, çünkü ilerleme geri
    /// gitmiyor; sınırlanması gereken tek yön ileri.
    /// </remarks>
    /// <returns>Kesirli parça indisi ve o noktaya olan uzaklık.</returns>
    private (float Index, float Distance) NearestAhead(float x, float y)
    {
        var current = (int)_progressIndex;

        var first = 0;
        var last = Math.Min(_points.Count - 2, current + Lookahead);

        var bestIndex = _progressIndex;
        var bestDistance = float.MaxValue;

        for (var i = first; i <= last; i++)
        {
            var (t, distance) = ProjectOnSegment(x, y, _points[i], _points[i + 1]);
            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i + t;
            }
        }

        return (bestIndex, bestDistance);
    }

    /// <summary>Noktanın parçaya izdüşümü: parça üstündeki oran ve uzaklık.</summary>
    private static (float T, float Distance) ProjectOnSegment(
        float x, float y, PathPoint from, PathPoint to)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var lengthSquared = (dx * dx) + (dy * dy);

        if (lengthSquared <= float.Epsilon)
        {
            return (0f, Distance(x, y, from.X, from.Y));
        }

        var t = Math.Clamp((((x - from.X) * dx) + ((y - from.Y) * dy)) / lengthSquared, 0f, 1f);
        var px = from.X + (t * dx);
        var py = from.Y + (t * dy);

        return (t, Distance(x, y, px, py));
    }

    private PathPoint PointAt(float index)
    {
        var i = Math.Clamp((int)index, 0, _points.Count - 2);
        var t = Math.Clamp(index - i, 0f, 1f);

        var from = _points[i];
        var to = _points[i + 1];

        return new PathPoint(
            from.X + ((to.X - from.X) * t),
            from.Y + ((to.Y - from.Y) * t));
    }

    private static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }

    /// <summary>Yeni bir yol üretir ve ilerlemeyi sıfırlar.</summary>
    private void BuildPath()
    {
        var shapes = MazeTraceTuning.Shapes.For(Band);
        Shape = shapes[_rng.Next(shapes.Length)];

        // Kenar payı toleransı da kapsıyor: yolun kenarına yapıştığı yerde
        // çocuğun parmağı ekranın dışına taşmak zorunda kalmasın.
        var margin = Tolerance + 0.07f;
        var span = 1f - (2f * margin);

        // Taşıyıcı eksen köşeden köşeye, yönü rastgele. Aynı yön hep
        // tekrarlanırsa dördüncü yolda çocuk artık bakmadan çiziyor.
        var (from, to) = PickAxis(margin, span);

        var axisX = to.X - from.X;
        var axisY = to.Y - from.Y;
        var axisLength = MathF.Sqrt((axisX * axisX) + (axisY * axisY));

        // Ekseni dik kesen birim vektör; sapma bu yönde uygulanıyor.
        var normalX = -axisY / axisLength;
        var normalY = axisX / axisLength;

        var amplitude = AmplitudeFor(Shape) * span;
        var sign = _rng.Next(2) == 0 ? 1f : -1f;

        _points.Clear();
        for (var i = 0; i < Samples; i++)
        {
            var t = i / (float)(Samples - 1);
            var offset = sign * amplitude * Displacement(Shape, t);

            _points.Add(new PathPoint(
                Math.Clamp(from.X + (axisX * t) + (normalX * offset), 0f, 1f),
                Math.Clamp(from.Y + (axisY * t) + (normalY * offset), 0f, 1f)));
        }

        _progressIndex = 0f;
        _isOffPath = false;
        IsTracing = false;
    }

    /// <summary>Yolun iki ucu. Dört yönden biri seçiliyor.</summary>
    private (PathPoint From, PathPoint To) PickAxis(float margin, float span)
    {
        // Kıvrımlı biçimlerde sapma da yer kapladığı için eksen ortaya
        // çekiliyor; düz ve kamburda tam kenardan kenara gidiyor.
        var inset = margin + (AmplitudeFor(Shape) * span);
        var free = 1f - (2f * inset);

        var low = inset + ((float)_rng.NextDouble() * free * 0.35f);
        var high = 1f - inset - ((float)_rng.NextDouble() * free * 0.35f);

        return _rng.Next(4) switch
        {
            0 => (new PathPoint(margin, low), new PathPoint(1f - margin, high)),
            1 => (new PathPoint(margin, high), new PathPoint(1f - margin, low)),
            2 => (new PathPoint(low, margin), new PathPoint(high, 1f - margin)),
            _ => (new PathPoint(high, margin), new PathPoint(low, 1f - margin)),
        };
    }

    private float AmplitudeFor(PathShape shape) => shape switch
    {
        PathShape.Straight => 0f,
        PathShape.Arc => 0.16f,
        PathShape.Wave => 0.13f,
        PathShape.Zigzag => 0.15f,
        _ => 0f,
    };

    /// <summary>Kaç tam kıvrım olacağı.</summary>
    private int Cycles => Math.Max(1, (int)MathF.Round(Complexity));

    /// <summary>
    /// Eksene dik sapma, 0-1 arası konuma göre.
    /// </summary>
    /// <remarks>
    /// Dalga ve zikzak tam sayıda kıvrım yapıyor: t=0 ve t=1'de sapma
    /// sıfır, yani yol her zaman ekseninin iki ucundan başlayıp bitiyor ve
    /// başlangıç/bitiş işaretleri yolun gerçekten üstünde duruyor.
    /// </remarks>
    private float Displacement(PathShape shape, float t) => shape switch
    {
        PathShape.Straight => 0f,

        // Tek kambur: iki uçta sıfır, ortada en yüksek.
        PathShape.Arc => MathF.Sin(t * MathF.PI),

        PathShape.Wave => MathF.Sin(t * MathF.Tau * Cycles),

        // Aynı kıvrım, yuvarlak yerine keskin: her kıvrımda iki köşe.
        PathShape.Zigzag => Triangle(t * Cycles),

        _ => 0f,
    };

    /// <summary>
    /// Üçgen dalga: sinüsün keskin köşeli hâli.
    /// </summary>
    /// <remarks>
    /// Bir tam kıvrımda 0 → 1 → 0 → -1 → 0 gidiyor, yani iki köşe üretiyor.
    /// Köşe bu oyunun asıl zorluğu: parmağın durup yön değiştirmesi
    /// gerekiyor, oysa yumuşak eğri parmağı kendi taşıyor.
    /// </remarks>
    private static float Triangle(float phase)
    {
        var local = phase - MathF.Floor(phase);

        return local switch
        {
            < 0.25f => local * 4f,
            < 0.75f => 2f - (local * 4f),
            _ => (local * 4f) - 4f,
        };
    }
}
