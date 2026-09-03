using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Bir dokunuşun sonucu.</summary>
public enum DotTapResult
{
    /// <summary>Hiçbir noktaya yakın değil ya da tur bitmiş — sayılmıyor.</summary>
    Ignored,

    /// <summary>Sıradaki noktaya dokunuldu, çizgi uzadı.</summary>
    Connected,

    /// <summary>Bir noktaya dokunuldu ama sıradaki o değildi.</summary>
    Wrong,

    /// <summary>Son noktaya dokunuldu; hat kapandı, resim bitti.</summary>
    PictureComplete,
}

/// <summary>Noktaları Birleştir'in banda göre ayarları.</summary>
public static class DotToDotTuning
{
    /// <summary>Bir turda kaç resim çiziliyor.</summary>
    /// <remarks>
    /// Meşe'nin resimleri iki kat çok noktalı, yani aynı sayıda resim iki kat
    /// uzun bir tur ederdi. Sayı bu yüzden bantla birlikte artmıyor.
    /// </remarks>
    public static readonly BandValue<int> Pictures = new(2, 3, 3);

    /// <summary>Resmin en az kaç noktası olabilir.</summary>
    public static readonly BandValue<int> MinDots = new(6, 8, 11);

    /// <summary>Resmin en çok kaç noktası olabilir.</summary>
    /// <remarks>
    /// Fidan'ın üst sınırı on: on birinci noktadan sonra rakamlar ekranda
    /// birbirine yaklaşıyor ve oyun sayı tanımaktan çok nokta ayırt etmeye
    /// dönüyor.
    /// </remarks>
    public static readonly BandValue<int> MaxDots = new(8, 10, 18);

    /// <summary>
    /// Sıradaki nokta ekranda belirtiliyor mu?
    /// </summary>
    /// <remarks>
    /// Fidan'da beliriyor: 4-6 yaş rakamları tanıyor ama sırayı ekranda
    /// aramak ayrı bir iş ve oyunun asıl öğrettiği "birden ona doğru gitmek".
    /// Meşe'de belirtme kapalı — orada oyun gerçekten rakam okumak.
    /// </remarks>
    public static readonly BandValue<bool> HighlightsNext = new(true, true, false);

    /// <summary>
    /// Noktanın dokunma yarıçapı, ekranın kısa kenarına oranla.
    /// </summary>
    /// <remarks>
    /// Meşe'de bile 0,06: yedi yüz piksellik bir kenarda 42 piksel yarıçap,
    /// yani 84 piksel çap. Tasarımdaki en küçük dokunma hedefi (64) bunun
    /// altında kalıyor, yani en zor bantta bile isabet parmağın değil sıranın
    /// meselesi.
    /// </remarks>
    public static readonly BandValue<float> Tolerance = new(0.10f, 0.08f, 0.06f);

    /// <summary>Yanlış noktaya dokunmak yıldızı düşürüyor mu?</summary>
    /// <remarks>
    /// Yalnızca Meşe'de. Küçük bantlarda yanlış nokta bir hata değil, sırayı
    /// arama biçimi; onu cezalandırmak çocuğu denemekten alıkoyuyor.
    /// </remarks>
    public static readonly BandValue<bool> CountsWrongTaps = new(false, false, true);
}

/// <summary>
/// Noktaları Birleştir turu: rakamlar sırayla takip edilerek bir hayvan çiziliyor.
/// </summary>
/// <remarks>
/// <para>
/// Kütüphanedeki diğer sayı oyunlarından farkı ne öğrettiği. Sayı Avı bir
/// rakamı <b>tanımayı</b>, Say ve Eşleştir miktarla rakamı <b>eşlemeyi</b>
/// çalıştırıyor. Burada çalışılan şey <b>sıra</b>: birden sonra iki gelir,
/// ondan sonra üç. Sayı doğrusu fikrinin kendisi bu ve bir sonraki adım
/// (toplama) onun üstüne kuruluyor.
/// </para>
/// <para>
/// Ödülü de kendine ait: çocuk sırayı doğru takip ettiğinde ekranda bir
/// hayvan beliriyor. Yıldızdan farklı olarak bu ödül <b>çizimin kendisi</b> —
/// çocuk onu yaptığını görüyor.
/// </para>
/// <para>
/// Boşluğa dokunmak hata değil (<see cref="DotTapResult.Ignored"/>). Dört
/// yaşındaki bir çocuğun parmağı ekranda geziniyor; her temasa hata yazmak
/// yıldızı beceriyle ilgisiz bir şeye bağlardı. Yalnızca <b>başka bir
/// noktaya</b> dokunmak yanlış sayılıyor, o da yalnızca Meşe'de.
/// </para>
/// </remarks>
public sealed class DotToDotRound
{
    private readonly List<DotPicture> _queue;

    private int _index;
    private int _wrongTaps;

    private DotToDotRound(AgeBand band, IReadOnlyList<DotPicture> pictures)
    {
        Band = band;
        Tolerance = DotToDotTuning.Tolerance.For(band);
        HighlightsNext = DotToDotTuning.HighlightsNext.For(band);
        CountsWrongTaps = DotToDotTuning.CountsWrongTaps.For(band);

        _queue = [.. pictures];
        Total = _queue.Count;
    }

    /// <summary>
    /// Bant için bir tur kurar.
    /// </summary>
    /// <remarks>
    /// Bandın aralığına hiç resim düşmezse en yakın olanlara iniliyor:
    /// kütüphane büyürken aralıklar kaydırılabilir ve o an boş kalan bir bant
    /// oyunu çöktürmemeli.
    /// </remarks>
    public static DotToDotRound ForBand(AgeBand band, Random? random = null)
    {
        var rng = random ?? Random.Shared;

        var pool = DotPictures.Between(
            DotToDotTuning.MinDots.For(band),
            DotToDotTuning.MaxDots.For(band));

        if (pool.Count == 0)
        {
            pool = DotPictures.All;
        }

        var wanted = DotToDotTuning.Pictures.For(band);

        // Havuz istenenden küçükse tekrar var ama arka arkaya aynı resim
        // gelmiyor: karıştırılmış havuz baştan sona geziliyor.
        var picked = new List<DotPicture>(wanted);
        while (picked.Count < wanted)
        {
            var shuffled = pool.OrderBy(_ => rng.Next()).ToList();
            picked.AddRange(shuffled.Take(wanted - picked.Count));
        }

        return new DotToDotRound(band, picked);
    }

    public AgeBand Band { get; }

    /// <summary>Turdaki resim sayısı.</summary>
    public int Total { get; }

    /// <summary>Bitirilmiş resim sayısı.</summary>
    public int Completed { get; private set; }

    public float Tolerance { get; }

    public bool HighlightsNext { get; }

    public bool CountsWrongTaps { get; }

    /// <summary>Şu an çizilen resim.</summary>
    public DotPicture Current => _queue[Math.Min(_index, _queue.Count - 1)];

    /// <summary>Sıradaki noktanın sırası (0'dan başlar); resim bittiyse nokta sayısına eşit.</summary>
    public int NextDot { get; private set; }

    /// <summary>Bağlanmış nokta sayısı — arayüz çizgiyi buraya kadar çiziyor.</summary>
    public int Connected => NextDot;

    /// <summary>Yanlış noktaya dokunma sayısı, sayılıp sayılmadığından bağımsız.</summary>
    public int WrongTaps => _wrongTaps;

    /// <summary>Yıldız hesabına giden hata sayısı.</summary>
    public int Mistakes => CountsWrongTaps ? _wrongTaps : 0;

    public bool IsComplete => Completed >= Total;

    /// <summary>Son <see cref="Tap"/> çağrısı resmi bitirdi mi?</summary>
    public bool PictureComplete { get; private set; }

    /// <summary>
    /// Ekrana dokunur.
    /// </summary>
    /// <param name="x">0-1 arası, ekranın soluna göre.</param>
    /// <param name="y">0-1 arası, ekranın üstüne göre.</param>
    public DotTapResult Tap(float x, float y)
    {
        PictureComplete = false;

        if (IsComplete)
        {
            return DotTapResult.Ignored;
        }

        var picture = Current;
        var nearest = NearestDot(picture, x, y);

        if (nearest < 0)
        {
            // Boşluğa dokunuldu. Hata değil: parmak ekranda geziniyor.
            return DotTapResult.Ignored;
        }

        if (nearest != NextDot)
        {
            _wrongTaps++;
            return DotTapResult.Wrong;
        }

        NextDot++;

        if (NextDot < picture.Count)
        {
            return DotTapResult.Connected;
        }

        // Son nokta bağlandı; hat birinciye kendiliğinden kapanıyor.
        PictureComplete = true;
        Completed++;
        _index++;
        NextDot = 0;

        return DotTapResult.PictureComplete;
    }

    /// <summary>
    /// Dokunulan noktanın sırası; hiçbiri yeterince yakın değilse -1.
    /// </summary>
    /// <remarks>
    /// <b>En yakın</b> nokta seçiliyor, ilk yeterince yakın olan değil. İki
    /// nokta yan yanaysa (yengecin kıskaçları) parmağın gerçekten hangisine
    /// bastığı ancak böyle çıkıyor.
    /// </remarks>
    private int NearestDot(DotPicture picture, float x, float y)
    {
        var best = -1;
        var bestDistance = Tolerance * Tolerance;

        for (var i = 0; i < picture.Count; i++)
        {
            var dot = picture.Dots[i];
            var dx = dot.X - x;
            var dy = dot.Y - y;
            var distance = (dx * dx) + (dy * dy);

            if (distance <= bestDistance)
            {
                best = i;
                bestDistance = distance;
            }
        }

        return best;
    }
}
