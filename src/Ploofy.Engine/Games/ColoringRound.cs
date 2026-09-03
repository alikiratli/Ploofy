using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Boyama dokunuşunun sonucu.</summary>
public enum PaintOutcome
{
    /// <summary>Boşluğa dokunuldu ya da tur bitti — hiçbir şey olmadı.</summary>
    Missed,

    /// <summary>Bir alan boyandı ya da rengi değişti.</summary>
    Painted,

    /// <summary>Son boş alan da boyandı; resim tamamlandı.</summary>
    PictureComplete,
}

/// <summary>Boyama'nın banda göre ayarları.</summary>
public static class ColoringTuning
{
    /// <summary>Bir turda kaç resim boyanıyor.</summary>
    public static readonly BandValue<int> Pictures = new(2, 2, 3);

    /// <summary>
    /// Resmin en çok kaç alanı olabilir.
    /// </summary>
    /// <remarks>
    /// Bu oyunun tek bant ekseni. Serbest oyunda "zorluk" diye bir şey yok —
    /// kaybetmek de yok, süre de yok — ama on alanlı bir çiçek iki yaşındaki
    /// çocuğun bitiremeyeceği kadar uzun. Filiz beş alana kadar olan
    /// sayfaları görüyor.
    /// </remarks>
    public static readonly BandValue<int> MaxRegions = new(5, 9, 9);

    /// <summary>Paletteki renk sayısı.</summary>
    /// <remarks>
    /// Altı, uygulamanın her yerinde dolaşan palet. Daha fazlası yatay
    /// ekranda dokunma hedefini küçültüyor; daha azı boyamayı kısıtlıyor.
    /// </remarks>
    public const int PaletteSize = 6;
}

/// <summary>
/// Boyama turu.
/// </summary>
/// <remarks>
/// <para>
/// Kütüphanedeki tek <b>serbest</b> oyun: doğru cevap yok, yanlış cevap yok,
/// süre yok, hata sayacı yok. Bir alanı istediği renge boyayan çocuk hiçbir
/// zaman yanlış yapmıyor ve boyadığı rengi istediği kadar değiştirebiliyor.
/// Bu yaşta (özellikle Filiz bandında) oyunun asıl işi bu — kaybetme
/// ihtimalinin hiç olmadığı bir alan.
/// </para>
/// <para>
/// "Bitmek" yine de var: bütün alanlar bir kez boyanınca resim tamamlanıyor
/// ve sıradakine geçiliyor. Bitişi olmayan bir oyun, tur sonu ekranına ve
/// yıldıza bağlanamazdı; çocuğun emeğinin karşılıksız kalması ise serbest
/// oyunun amacına ters.
/// </para>
/// <para>
/// Yıldız her zaman üç çıkıyor (hata sıfır, tur tamamlanmış). Bu bir yıldız
/// çiftliği değil: ilerleme kaydı oyun ve bant başına <b>en iyi</b> yıldızı
/// tutuyor, yani boyama toplam yıldıza en fazla üç ekliyor.
/// </para>
/// </remarks>
public sealed class ColoringRound
{
    private readonly List<ColoringPicture> _queue;
    private readonly Dictionary<string, int> _fills = new(StringComparer.Ordinal);

    private int _index;

    private ColoringRound(AgeBand band, List<ColoringPicture> pictures)
    {
        Band = band;
        _queue = pictures;
        Total = pictures.Count;
    }

    /// <summary>
    /// Bant için bir tur kurar.
    /// </summary>
    /// <remarks>
    /// Bandın sınırına hiç sayfa düşmezse bütün kütüphaneye iniliyor:
    /// sayfalar ileride değişebilir ve o an boş kalan bir bant oyunu
    /// çöktürmemeli.
    /// </remarks>
    public static ColoringRound ForBand(AgeBand band, Random? random = null)
    {
        var rng = random ?? Random.Shared;

        var pool = ColoringPictures.UpTo(ColoringTuning.MaxRegions.For(band));
        if (pool.Count == 0)
        {
            pool = ColoringPictures.All;
        }

        var wanted = ColoringTuning.Pictures.For(band);

        var picked = new List<ColoringPicture>(wanted);
        while (picked.Count < wanted)
        {
            var shuffled = pool.OrderBy(_ => rng.Next()).ToList();
            picked.AddRange(shuffled.Take(wanted - picked.Count));
        }

        return new ColoringRound(band, picked);
    }

    public AgeBand Band { get; }

    /// <summary>Turdaki resim sayısı.</summary>
    public int Total { get; }

    /// <summary>Tamamlanmış resim sayısı.</summary>
    public int Completed { get; private set; }

    /// <summary>Şu an boyanan resim.</summary>
    public ColoringPicture Current => _queue[Math.Min(_index, _queue.Count - 1)];

    /// <summary>Seçili renk — palet sırası.</summary>
    public int SelectedColor { get; private set; }

    /// <summary>Alan anahtarı -> renk sırası. Yalnızca boyanmış alanlar var.</summary>
    public IReadOnlyDictionary<string, int> Fills => _fills;

    /// <summary>Bu resimde kaç alan boyandı.</summary>
    public int PaintedRegions => _fills.Count;

    public bool IsComplete => Completed >= Total;

    /// <summary>Son <see cref="Paint"/> çağrısı resmi bitirdi mi?</summary>
    public bool PictureComplete { get; private set; }

    /// <summary>Palet seçimi. Aralık dışı değer yok sayılıyor.</summary>
    public void SelectColor(int index)
    {
        if (index >= 0 && index < ColoringTuning.PaletteSize)
        {
            SelectedColor = index;
        }
    }

    /// <summary>Alanın rengi; boyanmadıysa null.</summary>
    public int? ColorOf(string regionId) =>
        _fills.TryGetValue(regionId, out var color) ? color : null;

    /// <summary>
    /// Ekrana dokunur ve altındaki alanı seçili renge boyar.
    /// </summary>
    /// <remarks>
    /// Boyanmış bir alana tekrar dokunmak rengini <b>değiştiriyor</b>, hata
    /// sayılmıyor: fikir değiştirmek serbest oyunun kendisi. Resim yalnızca
    /// her alan en az bir kez boyandığında tamamlanıyor, yani renk
    /// değiştirmek bitişi geri almıyor.
    /// </remarks>
    public PaintOutcome Paint(float x, float y)
    {
        PictureComplete = false;

        if (IsComplete)
        {
            return PaintOutcome.Missed;
        }

        var picture = Current;
        if (picture.HitTest(x, y) is not { } region)
        {
            return PaintOutcome.Missed;
        }

        _fills[region.Id] = SelectedColor;

        if (_fills.Count < picture.RegionCount)
        {
            return PaintOutcome.Painted;
        }

        PictureComplete = true;
        Completed++;
        _index++;
        _fills.Clear();

        return PaintOutcome.PictureComplete;
    }
}
