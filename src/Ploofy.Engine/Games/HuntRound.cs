using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Avlanan şey: harf mi, sayı mı?</summary>
/// <remarks>
/// İki oyun aynı mekaniği paylaşıyor ama katalogda ayrı duruyor: çocuk "harf
/// oyunu"nu ve "sayı oyunu"nu ayrı seçmek istiyor, ebeveyn de neyin
/// çalışıldığını görmek istiyor.
/// </remarks>
public enum HuntKind
{
    Letter,
    Number,
}

/// <summary>Bir seçeneğe dokunmanın sonucu.</summary>
public enum HuntOutcome
{
    Ignored,
    Correct,
    Wrong,
}

/// <summary>Ekrandaki tek seçenek.</summary>
public sealed record HuntChoice(int Id, string Glyph, bool IsTarget);

/// <summary>Tek bir soru: aranan işaret ve seçenekler.</summary>
public sealed record HuntQuestion(string Target, IReadOnlyList<HuntChoice> Choices);

/// <summary>Harf/Sayı Avı'nın banda göre zorluk tablosu.</summary>
public static class HuntTuning
{
    /// <summary>Turdaki soru sayısı.</summary>
    public static readonly BandValue<int> Questions = new(4, 6, 10);

    /// <summary>Ekrandaki seçenek sayısı.</summary>
    public static readonly BandValue<int> Choices = new(2, 4, 6);

    /// <summary>
    /// Çeldiriciler hedefe benzeyenlerden mi seçilsin?
    /// </summary>
    /// <remarks>
    /// Bandın asıl farkı bu. Rastgele bir çeldirici arasından doğru harfi
    /// bulmak <b>tanıma</b>; b ile d arasından bulmak <b>ayırt etme</b>.
    /// İkincisi okumaya geçişin gerçek eşiği ve ancak Meşe'de anlamlı.
    /// </remarks>
    public static readonly BandValue<bool> UseConfusables = new(false, false, true);

    /// <summary>Üçüncü yıldız için hedef süre (yalnızca Meşe).</summary>
    public static readonly BandValue<TimeSpan?> ParTime = new(
        null,
        null,
        TimeSpan.FromSeconds(40));
}

/// <summary>
/// Harf Avı ve Sayı Avı'nın kuralları — arayüzden bağımsız.
/// </summary>
/// <remarks>
/// <para>
/// Aranan işaret büyük olarak gösteriliyor, altında seçenekler duruyor.
/// Yönerge yazıya bağlı değil: çocuk yukarıdaki işaretin aynısını aşağıda
/// buluyor. Bu, okuma bilmeyen Fidan bandının da oynayabilmesini sağlıyor.
/// </para>
/// <para>
/// İşaret havuzu dışarıdan veriliyor. Sebebi: alfabe dile göre değişiyor
/// (Türkçe'de Ç, Ğ, İ, Ö, Ş, Ü; Almanca'da Ä, Ö, Ü, ß) ve motorun dil
/// bilmesi gerekmiyor.
/// </para>
/// </remarks>
public sealed class HuntRound
{
    /// <summary>
    /// Birbirine benzeyen işaretler.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Karışması <b>görsel</b> olanlar: ayna simetrisi (b/d, p/q), dönme
    /// (6/9, M/W, n/u), benzer siluet (1/7) ve <b>aksan farkı</b> (O/Ö, C/Ç,
    /// I/İ). Sesçe benzeyenler burada değil — bu oyun sesi değil şekli
    /// ayırt ettiriyor.
    /// </para>
    /// <para>
    /// Aksanlı çiftler Türkçe ve Almanca için asıl kısım: Türkçe'de I/İ ve
    /// ı/i ayrımı okumaya geçişin en çok takılınan yeri, ve tam olarak bu
    /// oyunun çözmesi gereken şey. Havuzda bulunmayan bir işaret zaten
    /// elenıyor, o yüzden İngilizce turda bu satırlar kendiliğinden devre
    /// dışı kalıyor.
    /// </para>
    /// </remarks>
    private static readonly Dictionary<string, string[]> Confusables = new(StringComparer.Ordinal)
    {
        // Ayna ve dönme — küçük harfler
        ["b"] = ["d", "p", "q"],
        ["d"] = ["b", "p", "q"],
        ["p"] = ["q", "b", "d"],
        ["q"] = ["p", "b", "d"],
        ["n"] = ["u", "h", "m"],
        ["u"] = ["n", "v", "ü"],
        ["m"] = ["w", "n"],
        ["w"] = ["m", "v"],
        ["v"] = ["u", "w", "y"],
        ["h"] = ["n", "b", "k"],
        ["g"] = ["q", "y", "ğ"],
        ["a"] = ["e", "o", "ä"],
        ["e"] = ["a", "c"],
        ["c"] = ["e", "o", "ç"],
        ["o"] = ["c", "e", "ö"],
        ["s"] = ["z", "ş"],
        ["z"] = ["s"],
        ["t"] = ["f", "l"],
        ["f"] = ["t"],
        ["k"] = ["x", "h"],
        ["x"] = ["k", "y"],
        ["y"] = ["g", "v", "x"],
        ["r"] = ["n", "v"],
        ["l"] = ["i", "t", "ı"],
        ["j"] = ["i", "l"],

        // Ayna ve dönme — büyük harfler
        ["M"] = ["W", "N"],
        ["W"] = ["M", "V"],
        ["N"] = ["M", "Z", "H"],
        ["Z"] = ["N", "S"],
        ["E"] = ["F", "B"],
        ["F"] = ["E", "P", "T"],
        ["C"] = ["G", "O", "Ç"],
        ["G"] = ["C", "O", "Ğ"],
        ["O"] = ["Q", "C", "D", "Ö"],
        ["P"] = ["R", "F", "B"],
        ["R"] = ["P", "B"],
        ["B"] = ["R", "P", "D"],
        ["D"] = ["O", "B", "P"],
        ["H"] = ["N", "K"],
        ["K"] = ["X", "H"],
        ["X"] = ["K", "Y"],
        ["Y"] = ["V", "X"],
        ["T"] = ["I", "L", "F"],
        ["L"] = ["I", "T", "J"],
        ["J"] = ["L", "I"],
        ["V"] = ["W", "Y", "U"],
        ["U"] = ["V", "Ü", "N"],
        ["S"] = ["Ş", "Z"],
        ["A"] = ["Ä", "R", "H"],

        // Aksanlı çiftler — Türkçe'nin ve Almanca'nın asıl zorluğu
        ["I"] = ["İ", "T", "L"],
        ["İ"] = ["I", "T", "J"],
        ["ı"] = ["i", "l", "ï"],
        ["i"] = ["ı", "j", "l"],
        ["Ö"] = ["O", "Q"],
        ["ö"] = ["o", "ó"],
        ["Ü"] = ["U", "V"],
        ["ü"] = ["u", "v"],
        ["Ç"] = ["C", "G"],
        ["ç"] = ["c", "e"],
        ["Ş"] = ["S", "Z"],
        ["ş"] = ["s", "z"],
        ["Ğ"] = ["G", "C"],
        ["ğ"] = ["g", "q"],
        ["Ä"] = ["A", "Ö"],
        ["ä"] = ["a", "ö"],
        ["ß"] = ["B", "b"],

        // Sayılar
        ["6"] = ["9", "8", "5"],
        ["9"] = ["6", "8", "0"],
        ["2"] = ["5", "7", "3"],
        ["5"] = ["2", "S", "6"],
        ["3"] = ["8", "2"],
        ["8"] = ["3", "6", "9"],
        ["1"] = ["7", "4"],
        ["7"] = ["1", "2"],
        ["0"] = ["8", "9", "6"],
        ["4"] = ["1", "9"],
        // İki basamaklılar yalnızca Meşe havuzunda (0-20) var; rakam sırası
        // karışması bu yaşta gerçek bir hata kaynağı.
        ["12"] = ["21", "2", "13"],
        ["21"] = ["12", "20", "2"],
        ["13"] = ["3", "18", "12"],
        ["18"] = ["13", "8", "19"],
    };

    private readonly Random _rng;
    private readonly List<string> _pool;
    private readonly int _choiceCount;
    private readonly bool _useConfusables;

    private HuntRound(AgeBand band, HuntKind kind, List<string> pool, Random rng)
    {
        Band = band;
        Kind = kind;
        _pool = pool;
        _rng = rng;

        Total = HuntTuning.Questions.For(band);
        _choiceCount = Math.Min(HuntTuning.Choices.For(band), pool.Count);
        _useConfusables = HuntTuning.UseConfusables.For(band);
        ParTime = HuntTuning.ParTime.For(band);

        Current = BuildQuestion();
    }

    /// <summary>
    /// Bant için standart bir tur kurar.
    /// </summary>
    /// <param name="pool">
    /// Aranabilecek işaretler. Dile göre değiştiği için uygulama veriyor.
    /// </param>
    public static HuntRound ForBand(
        AgeBand band,
        HuntKind kind,
        IReadOnlyList<string> pool,
        Random? random = null)
    {
        if (pool.Count < 2)
        {
            throw new ArgumentException(
                "Av oyunu için en az iki işaret gerekiyor.", nameof(pool));
        }

        return new HuntRound(band, kind, pool.ToList(), random ?? Random.Shared);
    }

    public AgeBand Band { get; }

    public HuntKind Kind { get; }

    public TimeSpan? ParTime { get; }

    /// <summary>Turdaki toplam soru sayısı.</summary>
    public int Total { get; }

    public int Correct { get; private set; }

    public int Mistakes { get; private set; }

    /// <summary>Sıradaki soru; tur bittiyse null.</summary>
    public HuntQuestion? Current { get; private set; }

    public bool IsComplete => Correct >= Total;

    /// <summary>
    /// Bir seçeneğe dokunur.
    /// </summary>
    /// <remarks>
    /// Yanlış seçenekte soru <b>değişmiyor</b>: çocuk aynı soruyu doğru
    /// cevaplayana kadar deneyebiliyor. Doğru cevabı görmeden geçmek, oyunun
    /// öğretici olma iddiasını boşa çıkarırdı.
    /// </remarks>
    public HuntOutcome Tap(int choiceId)
    {
        if (Current is not { } question)
        {
            return HuntOutcome.Ignored;
        }

        var choice = question.Choices.FirstOrDefault(c => c.Id == choiceId);
        if (choice is null)
        {
            return HuntOutcome.Ignored;
        }

        if (!choice.IsTarget)
        {
            // Filiz bandında hata sayılmıyor; zaten bu oyun Fidan'dan
            // itibaren katalogda görünüyor ama motor bandı varsayamaz.
            if (Band != AgeBand.Filiz)
            {
                Mistakes++;
            }

            return HuntOutcome.Wrong;
        }

        Correct++;
        Current = IsComplete ? null : BuildQuestion();
        return HuntOutcome.Correct;
    }

    private HuntQuestion BuildQuestion()
    {
        var target = _pool[_rng.Next(_pool.Count)];

        var glyphs = new List<string> { target };
        foreach (var distractor in PickDistractors(target))
        {
            glyphs.Add(distractor);
        }

        Shuffle(glyphs);

        var id = 0;
        var choices = glyphs
            .Select(g => new HuntChoice(id++, g, string.Equals(g, target, StringComparison.Ordinal)))
            .ToList();

        return new HuntQuestion(target, choices);
    }

    private IEnumerable<string> PickDistractors(string target)
    {
        var needed = _choiceCount - 1;
        var chosen = new List<string>(needed);

        if (_useConfusables && Confusables.TryGetValue(target, out var similar))
        {
            // Benzerlerden yalnızca havuzda gerçekten bulunanlar; havuz dile
            // göre değişiyor ve olmayan bir harfi göstermek anlamsız.
            foreach (var candidate in similar)
            {
                if (chosen.Count >= needed)
                {
                    break;
                }

                if (_pool.Contains(candidate, StringComparer.Ordinal))
                {
                    chosen.Add(candidate);
                }
            }
        }

        // Kalanı havuzdan rastgele tamamla.
        var remaining = _pool
            .Where(g => !string.Equals(g, target, StringComparison.Ordinal))
            .Where(g => !chosen.Contains(g, StringComparer.Ordinal))
            .OrderBy(_ => _rng.Next())
            .Take(needed - chosen.Count);

        chosen.AddRange(remaining);
        return chosen;
    }

    private void Shuffle(IList<string> items)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = _rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
