using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Bir tuşa dokunmanın sonucu.</summary>
public enum SimonOutcome
{
    /// <summary>Dokunma yok sayıldı (ekranda olmayan tuş, tur bitmiş).</summary>
    Ignored,

    /// <summary>Dizinin bir adımı doğru; devam ediliyor.</summary>
    Correct,

    /// <summary>Dizinin tamamı doğru tekrarlandı — dizi bir uzuyor.</summary>
    LevelComplete,

    /// <summary>Yanlış tuş — dizi baştan gösterilecek.</summary>
    Wrong,
}

/// <summary>Sırayı Tekrarla'nın banda göre zorluk tablosu.</summary>
public static class SimonTuning
{
    /// <summary>Ekrandaki tuş sayısı.</summary>
    /// <remarks>
    /// Üstteki sınır altı: renk paleti altı renk taşıyor ve tuşlar birbirinden
    /// yalnızca renkle ayrılıyor. Yedinci tuş, adı olmayan bir renk demek.
    /// </remarks>
    public static readonly BandValue<int> Pads = new(3, 4, 6);

    /// <summary>İlk seviyenin dizi uzunluğu.</summary>
    public static readonly BandValue<int> StartLength = new(1, 2, 3);

    /// <summary>Turu bitirmek için tamamlanması gereken seviye sayısı.</summary>
    /// <remarks>
    /// Son dizinin uzunluğu <c>StartLength + Levels - 1</c>: Filiz 4, Fidan 6,
    /// Meşe 8. Üst sınırlar yaşa göre akılda tutulabilen dizi uzunluğuna
    /// yakın seçildi — bir fazlası oyunu bitirilemez yapıyor.
    /// </remarks>
    public static readonly BandValue<int> Levels = new(4, 5, 6);

    /// <summary>Gösterim sırasında bir tuşun yanık kalma süresi.</summary>
    /// <remarks>
    /// Arayüz bunu okuyup diziyi bu hızda oynatıyor. Küçük bantta yavaş
    /// olması şart: hızlı gösterim ezberlenecek bir dizi değil, kaçırılmış
    /// bir ışık oyunu hâline geliyor.
    /// </remarks>
    public static readonly BandValue<TimeSpan> StepDuration = new(
        TimeSpan.FromMilliseconds(750),
        TimeSpan.FromMilliseconds(600),
        TimeSpan.FromMilliseconds(450));

    /// <summary>Aynı tuş dizide arka arkaya iki kez gelebilir mi?</summary>
    /// <remarks>
    /// Bandın sessiz ama en önemli farkı bu. Aynı tuşun peş peşe iki kez
    /// yanması, "iki kez mi yandı yoksa bir kez uzun mu?" sorusunu doğuruyor
    /// ve küçük çocuk bunu ayırt edemiyor — hata diziyi hatırlamamaktan değil
    /// gösterimi okuyamamaktan geliyor. Meşe'de ayırt edilebiliyor ve dizinin
    /// gerçek zorluğunu artırıyor.
    /// </remarks>
    public static readonly BandValue<bool> AllowsImmediateRepeat = new(false, false, true);

    // Hedef süre yok — Meşe'de bile. Bu turda geçen zamanın büyük kısmı
    // ekranın kendi gösterimi ve çocuk onu hızlandıramıyor. Süreyi ölçmek
    // hatırlamayı değil, dizi biter bitmez tahmin yürütmeyi ödüllendirirdi.
    // Meşe'nin üçüncü yıldızı bu oyunda hatasızlığa bağlı; bkz. StarRating.
}

/// <summary>
/// Sırayı Tekrarla'nın kuralları — arayüzden bağımsız.
/// </summary>
/// <remarks>
/// <para>
/// Ekran bir diziyi kendi oynatıyor (tuşlar sırayla yanıyor), sonra çocuk
/// aynı sırayla dokunuyor. Diğer bütün oyunlar "çocuk dokunur, ekran cevap
/// verir" kalıbındaydı; bu ilk kez tersi ve kütüphaneye eksik olan
/// <see cref="Catalog.InteractionKind.Sequence"/> türünü getiriyor.
/// </para>
/// <para>
/// Dizi her seviyede <b>uzuyor</b>, yeniden üretilmiyor: eski dizi yeninin
/// başlangıcı olarak kalıyor. Klasik oyunun bu kuralı tesadüf değil, çocuk
/// her seviyede tanıdığı bir başlangıcın üstüne tek bir şey ekliyor ve dizi
/// böyle ezberlenebilir kalıyor.
/// </para>
/// <para>
/// Zamanlama burada yok. Motor diziyi ve nerede kalındığını biliyor, ne
/// zaman yanıp söneceğini bilmiyor — o arayüzün işi.
/// <see cref="StepDuration"/> yalnızca zorluk tablosundan okunan bir sayı
/// olarak taşınıyor.
/// </para>
/// </remarks>
public sealed class SimonRound
{
    private readonly Random _rng;
    private readonly List<int> _sequence;
    private readonly bool _allowsImmediateRepeat;

    private SimonRound(AgeBand band, Random rng)
    {
        Band = band;
        _rng = rng;

        Pads = SimonTuning.Pads.For(band);
        Total = SimonTuning.Levels.For(band);
        StepDuration = SimonTuning.StepDuration.For(band);
        _allowsImmediateRepeat = SimonTuning.AllowsImmediateRepeat.For(band);

        var startLength = SimonTuning.StartLength.For(band);
        _sequence = new List<int>(startLength + Total);
        for (var i = 0; i < startLength; i++)
        {
            _sequence.Add(NextPad());
        }
    }

    /// <summary>Bant için standart bir tur kurar. <paramref name="random"/> testlerde sabitlenebilir.</summary>
    public static SimonRound ForBand(AgeBand band, Random? random = null) =>
        new(band, random ?? Random.Shared);

    public AgeBand Band { get; }

    /// <summary>Ekrandaki tuş sayısı. Tuşlar 0'dan başlayarak numaralı.</summary>
    public int Pads { get; }

    /// <summary>Gösterimde bir tuşun yanık kalma süresi; arayüz bunu okuyor.</summary>
    public TimeSpan StepDuration { get; }

    /// <summary>Turu bitirmek için gereken seviye sayısı.</summary>
    public int Total { get; }

    /// <summary>Tamamlanan seviye sayısı.</summary>
    public int Completed { get; private set; }

    public int Mistakes { get; private set; }

    /// <summary>Gösterilecek ve tekrarlanacak dizi.</summary>
    public IReadOnlyList<int> Sequence => _sequence;

    /// <summary>Dizinin kaçıncı adımı bekleniyor.</summary>
    public int Position { get; private set; }

    public bool IsComplete => Completed >= Total;

    /// <summary>
    /// Bir tuşa dokunur.
    /// </summary>
    /// <remarks>
    /// Yanlış tuşta dizi <b>değişmiyor</b>, yalnızca baştan gösteriliyor:
    /// çocuk aynı diziyi bir kez daha görüp yeniden deneyebiliyor. Yeni bir
    /// dizi üretmek, çocuğun tam da takıldığı şeyi elinden almak olurdu —
    /// Harf Avı ve Say ve Eşleştir'deki karar burada da geçerli.
    /// </remarks>
    public SimonOutcome Tap(int pad)
    {
        if (IsComplete || pad < 0 || pad >= Pads)
        {
            return SimonOutcome.Ignored;
        }

        if (pad != _sequence[Position])
        {
            // Filiz bandında hata sayılmıyor; bu oyun katalogda Filiz'den
            // itibaren görünüyor ve o bantta yanlış tuş öğrenmenin kendisi.
            if (Band != AgeBand.Filiz)
            {
                Mistakes++;
            }

            Position = 0;
            return SimonOutcome.Wrong;
        }

        Position++;

        if (Position < _sequence.Count)
        {
            return SimonOutcome.Correct;
        }

        Completed++;
        Position = 0;

        if (!IsComplete)
        {
            _sequence.Add(NextPad());
        }

        return SimonOutcome.LevelComplete;
    }

    private int NextPad()
    {
        if (_allowsImmediateRepeat || _sequence.Count == 0)
        {
            return _rng.Next(Pads);
        }

        // Son tuşu bir eksik aralıktan kaçınarak seçiyoruz: deneyip yeniden
        // çekmek yerine üstünü kaydırmak, kaçınmayı tesadüfe bırakmıyor.
        var last = _sequence[^1];
        var pick = _rng.Next(Pads - 1);
        return pick >= last ? pick + 1 : pick;
    }
}
