namespace Ploofy.Engine.Games;

/// <summary>
/// Parmakla takip edilen tek bir çizgi.
/// </summary>
/// <remarks>
/// <para>
/// Yolu Bul'un yol takibi ile Harf Yazma'nın kalem darbesi aynı şey: sıralı
/// noktalardan geçen bir çizgi, banda göre bir tolerans ve geri gitmeyen bir
/// ilerleme. Mekanik burada tek nüsha duruyor; iki oyun da bunu kullanıyor.
/// </para>
/// <para>
/// İlerleme <b>geri gitmiyor</b>. Parmak geriye kayarsa ilerleme olduğu yerde
/// kalıyor, çocuk yeniden ileri gidince kaldığı yerden devam ediyor. Geri
/// saymak, titreyen bir parmağın çizgiyi hiç bitirememesi demek olurdu.
/// </para>
/// <para>
/// Parmak kalkarsa ilerleme <b>korunuyor</b>; kaldığı yere yeniden dokunup
/// devam edebiliyor. Bu yüzden arayüzün <see cref="Head"/> noktasını açıkça
/// göstermesi şart — çocuk parmağını nereye koyacağını başka türlü bilemez.
/// </para>
/// </remarks>
public sealed class TracePath
{
    /// <summary>
    /// İlerlemenin bir hamlede atlayabileceği en fazla parça sayısı, yolun
    /// toplam parça sayısına oran olarak.
    /// </summary>
    /// <remarks>
    /// Parmağın çizgiye en yakın noktası yalnızca bu pencere içinde aranıyor.
    /// Penceresiz arama, çizgiye yakın geçen ileri bir bölüme atlamayı mümkün
    /// kılıyor: çocuk parmağını kaldırıp ortaya koyarak yarısını atlayabiliyordu.
    ///
    /// Oran olarak yazılmasının sebebi, farklı yolların farklı sıklıkta
    /// örneklenmesi: yüz yirmi noktalık bir yolda on parça neyse, otuz
    /// noktalık kısa bir harf darbesinde de aynı oran olmalı.
    /// </remarks>
    private const float LookaheadRatio = 10f / 119f;

    /// <summary>Çizginin bitmiş sayıldığı ilerleme oranı.</summary>
    /// <remarks>
    /// Son noktaya birebir değmesini beklemiyoruz: sona gelmiş bir çocuğun
    /// son birkaç pikseli de kovalaması, bitirmeyi bir beceriden inada
    /// çeviriyor.
    /// </remarks>
    private const float FinishAt = 0.96f;

    private readonly List<PathPoint> _points;
    private readonly int _lookahead;

    private float _progressIndex;
    private bool _isOffPath;

    public TracePath(IReadOnlyList<PathPoint> points, float tolerance)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(points.Count, 2);

        _points = [.. points];
        _lookahead = Math.Max(1, (int)MathF.Round((_points.Count - 1) * LookaheadRatio));

        Tolerance = tolerance;
    }

    /// <summary>Çizginin örneklenmiş noktaları, baştan sona.</summary>
    public IReadOnlyList<PathPoint> Points => _points;

    /// <summary>Çizgiden sayılan en büyük sapma.</summary>
    public float Tolerance { get; }

    /// <summary>Parmağın konabileceği alan, <see cref="Tolerance"/>'tan geniş.</summary>
    /// <remarks>
    /// Parmağı ince bir çizginin tam üstüne indirmek, onu takip etmekten daha
    /// zor: dokunmadan önce parmağın altını göremiyorsun.
    /// </remarks>
    public float GrabTolerance => Tolerance * 1.7f;

    public PathPoint Start => _points[0];

    public PathPoint Goal => _points[^1];

    /// <summary>İlerlemenin ucu — arayüz parmağın nereye konacağını buradan gösteriyor.</summary>
    public PathPoint Head => PointAt(_progressIndex);

    /// <summary>Çizginin ne kadarı geçildi (0-1).</summary>
    public float Progress => _progressIndex / (_points.Count - 1);

    /// <summary>Parmak çizgiyi tutuyor mu?</summary>
    public bool IsTracing { get; private set; }

    /// <summary>Parmak şu an çizginin dışında mı?</summary>
    public bool IsOffPath => IsTracing && _isOffPath;

    public bool IsFinished { get; private set; }

    /// <summary>Çizgiden çıkma sayısı — hata sayılıp sayılmadığından bağımsız.</summary>
    public int Slips { get; private set; }

    /// <summary>
    /// Parmağı çizgiye koyar.
    /// </summary>
    /// <remarks>
    /// Kabul edilen tek yer <see cref="Head"/> çevresi — çizginin başı değil.
    /// Yarısına kadar gelmiş bir çocuk başlangıca dokunursa hiçbir şey
    /// olmuyor, çünkü ilerleme geri gitmiyor ve oradan devam etmek yolun
    /// yarısını boşuna çizmek olurdu.
    /// </remarks>
    public TraceOutcome Begin(float x, float y)
    {
        if (IsFinished)
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

    /// <summary>
    /// Parmağı çizgi boyunca taşır.
    /// </summary>
    /// <returns>
    /// Çizgi bittiğinde <see cref="TraceOutcome.LevelComplete"/>. Bunun ne
    /// anlama geldiğine sahibi karar veriyor: Yolu Bul'da bir yol biter,
    /// Harf Yazma'da harfin bir darbesi.
    /// </returns>
    public TraceOutcome MoveTo(float x, float y)
    {
        if (!IsTracing || IsFinished)
        {
            return TraceOutcome.Ignored;
        }

        var (index, distance) = NearestAhead(x, y);

        if (distance > Tolerance)
        {
            // Bir çıkış bir hata. Her karede saymak, çizginin dışında duran
            // bir parmağı saniyede altmış hataya çeviriyordu.
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
        IsFinished = true;
        IsTracing = false;

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
    /// Parmağın çizgiye en yakın olduğu yer — ileriye doğru pencere içinde.
    /// </summary>
    /// <remarks>
    /// Pencere yalnızca <b>ileriyi</b> sınırlıyor; geriye doğru çizginin
    /// tamamı açık. Sebebi cihazda görüldü: dar bir geri pay (iki parça,
    /// yaklaşık yolun yüzde ikisi) bırakıldığında köşede geriye kayan parmak
    /// yoldan çıkmış sayılıyordu — Meşe'de bu bir hata puanı, ve o kadar geri
    /// kayma beş yaşındaki bir çocuğun titremesi kadar bir mesafe. Geriye
    /// gitmek zaten hiçbir şey kazandırmıyor, çünkü ilerleme geri gitmiyor;
    /// sınırlanması gereken tek yön ileri.
    /// </remarks>
    /// <returns>Kesirli parça indisi ve o noktaya olan uzaklık.</returns>
    private (float Index, float Distance) NearestAhead(float x, float y)
    {
        var current = (int)_progressIndex;

        var last = Math.Min(_points.Count - 2, current + _lookahead);

        var bestIndex = _progressIndex;
        var bestDistance = float.MaxValue;

        for (var i = 0; i <= last; i++)
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

    internal static float Distance(float ax, float ay, float bx, float by)
    {
        var dx = ax - bx;
        var dy = ay - by;
        return MathF.Sqrt((dx * dx) + (dy * dy));
    }
}
