using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Harf Yazma'nın banda göre ayarları.</summary>
public static class LetterTraceTuning
{
    /// <summary>Bir turda kaç işaret yazılıyor.</summary>
    /// <remarks>
    /// Yolu Bul'dan az: bir harf birden çok darbe demek, yani aynı sayıda
    /// işaret çok daha uzun bir tur ediyor.
    ///
    /// Filiz bu oyunu hiç görmüyor — katalogdaki en küçük bant Fidan, çünkü
    /// 2-4 yaş harf yazmıyor. Yine de bir değer duruyor: sıfır işaretli bir
    /// tur, hiçbir şey yapmadan biten bir oyun demek olurdu.
    /// </remarks>
    public static readonly BandValue<int> Glyphs = new(3, 3, 5);

    /// <summary>
    /// Çizgiden sayılan en büyük sapma.
    /// </summary>
    /// <remarks>
    /// Yolu Bul'un toleransından biraz geniş (0,08 / 0,055 yerine
    /// 0,09 / 0,065). Sebebi: harf darbeleri kısa ve köşeli, yolun kıvrımları
    /// ise uzun ve yumuşak. Aynı toleransta harf belirgin biçimde daha zor
    /// oluyor ve zorluk beceriden değil biçimden geliyor.
    /// </remarks>
    public static readonly BandValue<float> Tolerance = new(0.12f, 0.09f, 0.065f);

    /// <summary>Çizgiden çıkmak yıldızı düşürüyor mu?</summary>
    /// <remarks>
    /// Yalnızca Meşe'de. Fidan'da amaç harfin şeklini tanımak; elin
    /// titremesini cezalandırmak yazmayı sevdirmenin tersi.
    /// </remarks>
    public static readonly BandValue<bool> CountsSlips = new(false, false, true);
}

/// <summary>
/// Harf Yazma turu: işaretin darbeleri sırayla parmakla çiziliyor.
/// </summary>
/// <remarks>
/// <para>
/// Yolu Bul yazı öncesi beceriyi (çizgi takip etmek) çalıştırıyor; burası
/// bir adım sonrası: <b>belirli</b> bir biçimi, <b>öğretilen sırayla</b>
/// çizmek. Darbe sırası dayatılıyor çünkü yanlış sırayla yazmayı öğrenen
/// çocuk bunu sonradan zor bırakıyor.
/// </para>
/// <para>
/// Takip mekaniği <see cref="TracePath"/>'te, Yolu Bul ile ortak. Buradaki
/// tek yeni şey darbelerin sırası: bir darbe bitmeden sonraki başlamıyor ve
/// arayüz bitenleri kalıcı olarak çizili gösteriyor — çocuk harfin ortaya
/// çıkışını görüyor, bütün mesele bu.
/// </para>
/// </remarks>
public sealed class LetterTraceRound
{
    private readonly List<Glyph> _queue;
    private readonly List<TracePath> _paths = [];

    private int _index;
    private int _slipsCarried;

    private LetterTraceRound(AgeBand band, IReadOnlyList<Glyph> glyphs)
    {
        Band = band;
        Tolerance = LetterTraceTuning.Tolerance.For(band);
        CountsSlips = LetterTraceTuning.CountsSlips.For(band);

        _queue = [.. glyphs];
        Total = _queue.Count;

        LoadGlyph();
    }

    /// <summary>
    /// Bant için bir tur kurar.
    /// </summary>
    /// <param name="pool">
    /// Yazılabilecek işaretler. Dile göre değiştiği için uygulama veriyor —
    /// motorun alfabe bilmesi gerekmiyor. Tanımlı bir yazım yolu olmayan
    /// işaretler sessizce eleniyor.
    /// </param>
    /// <exception cref="ArgumentException">Havuzda yazılabilir hiçbir işaret yoksa.</exception>
    public static LetterTraceRound ForBand(
        AgeBand band,
        IReadOnlyList<string> pool,
        Random? random = null)
    {
        var rng = random ?? Random.Shared;

        var writable = pool
            .Select(GlyphShapes.Find)
            .OfType<Glyph>()
            .ToList();

        if (writable.Count == 0)
        {
            throw new ArgumentException("Havuzda yazılabilir işaret yok.", nameof(pool));
        }

        var wanted = LetterTraceTuning.Glyphs.For(band);

        // Havuz istenen sayıdan küçükse tekrar var, ama arka arkaya aynı
        // harf gelmiyor: karıştırılmış havuz baştan sona geziliyor.
        var picked = new List<Glyph>(wanted);
        while (picked.Count < wanted)
        {
            var shuffled = writable.OrderBy(_ => rng.Next()).ToList();
            picked.AddRange(shuffled.Take(wanted - picked.Count));
        }

        return new LetterTraceRound(band, picked);
    }

    public AgeBand Band { get; }

    /// <summary>Turda yazılacak işaret sayısı.</summary>
    public int Total { get; }

    /// <summary>Bitirilmiş işaret sayısı.</summary>
    public int Completed { get; private set; }

    public float Tolerance { get; }

    public bool CountsSlips { get; }

    /// <summary>Şu an yazılan işaret.</summary>
    public Glyph Current => _queue[Math.Min(_index, _queue.Count - 1)];

    /// <summary>İşaretin bütün darbeleri — arayüz hepsini soluk gösteriyor.</summary>
    public IReadOnlyList<TracePath> Strokes => _paths;

    /// <summary>Sıradaki darbenin sırası; bitmiş olanlar bunun öncesinde.</summary>
    public int StrokeIndex { get; private set; }

    /// <summary>Şu an çizilen darbe; işaret bittiyse <c>null</c>.</summary>
    public TracePath? ActiveStroke =>
        StrokeIndex < _paths.Count ? _paths[StrokeIndex] : null;

    /// <summary>Çizgiden çıkma sayısı — hata sayılıp sayılmadığından bağımsız.</summary>
    public int Slips => _slipsCarried + _paths.Sum(p => p.Slips);

    /// <summary>Yıldız hesabına giden hata sayısı.</summary>
    public int Mistakes => CountsSlips ? Slips : 0;

    public bool IsComplete => Completed >= Total;

    /// <summary>Parmağı sıradaki darbenin ucuna koyar.</summary>
    public TraceOutcome Begin(float x, float y) =>
        IsComplete ? TraceOutcome.Ignored : ActiveStroke?.Begin(x, y) ?? TraceOutcome.Ignored;

    /// <summary>
    /// Parmağı darbe boyunca taşır.
    /// </summary>
    /// <returns>
    /// Bir darbe bittiğinde <see cref="TraceOutcome.LevelComplete"/>. İşaretin
    /// tamamı bittiyse <see cref="GlyphComplete"/> de <c>true</c> olur —
    /// arayüz kutlamayı ona bakarak yapıyor, her darbede değil.
    /// </returns>
    public TraceOutcome MoveTo(float x, float y)
    {
        if (IsComplete)
        {
            return TraceOutcome.Ignored;
        }

        GlyphComplete = false;

        var stroke = ActiveStroke;
        if (stroke is null)
        {
            return TraceOutcome.Ignored;
        }

        var outcome = stroke.MoveTo(x, y);
        if (outcome != TraceOutcome.LevelComplete)
        {
            return outcome;
        }

        StrokeIndex++;

        if (StrokeIndex < _paths.Count)
        {
            return TraceOutcome.LevelComplete;
        }

        // İşaretin son darbesi de bitti.
        GlyphComplete = true;
        Completed++;
        _index++;

        if (!IsComplete)
        {
            LoadGlyph();
        }

        return TraceOutcome.LevelComplete;
    }

    /// <summary>Son <see cref="MoveTo"/> çağrısı işaretin tamamını bitirdi mi?</summary>
    public bool GlyphComplete { get; private set; }

    /// <summary>Parmağı kaldırır. Hata değil, ilerleme silinmiyor.</summary>
    public void Release() => ActiveStroke?.Release();

    private void LoadGlyph()
    {
        _slipsCarried += _paths.Sum(p => p.Slips);

        _paths.Clear();
        foreach (var stroke in _queue[_index].Strokes)
        {
            _paths.Add(new TracePath(stroke, Tolerance));
        }

        StrokeIndex = 0;
    }
}
