using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Tek bir kart.</summary>
/// <param name="Index">Tahtadaki konumu (sıfırdan başlar).</param>
/// <param name="SymbolId">
/// Kartın üstündeki sembolün sabit anahtarı. Aynı anahtar iki kartta bulunur;
/// eşleşme bununla belirlenir.
/// </param>
public sealed record MemoryCard(int Index, string SymbolId);

/// <summary>Bir kart çevirmenin sonucu.</summary>
public enum FlipResult
{
    /// <summary>Çevirme kabul edildi, ikinci kart bekleniyor.</summary>
    AwaitingSecond,

    /// <summary>İki kart eşleşti.</summary>
    Matched,

    /// <summary>
    /// İki kart eşleşmedi; arayüz <see cref="MemoryMatchRound.MismatchReveal"/>
    /// kadar bekleyip <see cref="MemoryMatchRound.CloseMismatch"/> çağırmalı.
    /// </summary>
    Mismatched,

    /// <summary>Çevirme yok sayıldı (zaten açık kart, kapanma bekleniyor vb.).</summary>
    Ignored,
}

/// <summary>
/// Eşleştirme Kartları oyununun banda göre zorluk tablosu.
/// </summary>
/// <remarks>
/// Tek yerde durması bilinçli: bir bant ayarını denemek istendiğinde oyunun
/// mantığını okumaya gerek kalmıyor.
/// </remarks>
public static class MemoryMatchTuning
{
    /// <summary>Kaç çift kart olacağı.</summary>
    public static readonly BandValue<int> Pairs = new(3, 6, 10);

    /// <summary>
    /// Eşleşmeyen kartların açık kaldığı süre. Küçük yaşta uzun tutuluyor;
    /// kartın kapanma hızı bu bantta oyunun asıl zorluğu oluyor.
    /// </summary>
    public static readonly BandValue<TimeSpan> MismatchReveal = new(
        TimeSpan.FromMilliseconds(1600),
        TimeSpan.FromMilliseconds(1100),
        TimeSpan.FromMilliseconds(800));

    /// <summary>Üçüncü yıldız için hedef süre (yalnızca Meşe kullanır).</summary>
    public static readonly BandValue<TimeSpan?> ParTime = new(
        null,
        null,
        TimeSpan.FromSeconds(75));

    /// <summary>
    /// Tahtanın sütun sayısı — kart, küçük ekranda da parmakla rahat
    /// dokunulabilir kalsın diye banda bağlı.
    /// </summary>
    public static readonly BandValue<int> Columns = new(3, 4, 5);
}

/// <summary>
/// Eşleştirme Kartları oyununun bir turu — arayüzden bağımsız kurallar.
/// </summary>
/// <remarks>
/// Kart çevirme animasyonu ve dizilim sayfa tarafında; hangi kartların olduğu,
/// neyin eşleştiği ve turun bittiği burada. Bu ayrım sayesinde oyunun mantığı
/// MAUI olmadan test edilebiliyor ve aynı kurallar ileride farklı bir
/// yerleşimle (ör. yatay tablet düzeni) yeniden kullanılabiliyor.
/// </remarks>
public sealed class MemoryMatchRound
{
    private readonly List<MemoryCard> _cards;
    private readonly HashSet<int> _matched = [];
    private readonly List<int> _faceUp = [];

    private MemoryMatchRound(AgeBand band, List<MemoryCard> cards)
    {
        Band = band;
        _cards = cards;
        MismatchReveal = MemoryMatchTuning.MismatchReveal.For(band);
        ParTime = MemoryMatchTuning.ParTime.For(band);
        Columns = MemoryMatchTuning.Columns.For(band);
    }

    /// <summary>
    /// Bant için standart bir tur kurar. <paramref name="random"/> testlerde
    /// sabitlenebilir.
    /// </summary>
    public static MemoryMatchRound ForBand(
        AgeBand band,
        IReadOnlyList<string> symbolPool,
        Random? random = null)
    {
        var pairCount = MemoryMatchTuning.Pairs.For(band);
        if (symbolPool.Count < pairCount)
        {
            throw new ArgumentException(
                $"Bu bant {pairCount} çift istiyor, havuzda {symbolPool.Count} sembol var.",
                nameof(symbolPool));
        }

        var rng = random ?? Random.Shared;

        var pool = symbolPool.ToList();
        Shuffle(pool, rng);

        var deck = new List<string>(pairCount * 2);
        foreach (var symbol in pool.Take(pairCount))
        {
            deck.Add(symbol);
            deck.Add(symbol);
        }

        Shuffle(deck, rng);

        var cards = deck.Select((symbol, i) => new MemoryCard(i, symbol)).ToList();
        return new MemoryMatchRound(band, cards);
    }

    public AgeBand Band { get; }

    public IReadOnlyList<MemoryCard> Cards => _cards;

    public TimeSpan MismatchReveal { get; }

    public TimeSpan? ParTime { get; }

    public int Columns { get; }

    /// <summary>Eşleşmiş kartların konumları.</summary>
    public IReadOnlySet<int> MatchedIndices => _matched;

    /// <summary>Şu an açık duran (henüz eşleşmemiş) kartların konumları.</summary>
    public IReadOnlyList<int> FaceUpIndices => _faceUp;

    public int Mistakes { get; private set; }

    public int MatchedPairs => _matched.Count / 2;

    public int TotalPairs => _cards.Count / 2;

    public bool IsComplete => _matched.Count == _cards.Count;

    public bool IsRevealed(int index) => _matched.Contains(index) || _faceUp.Contains(index);

    /// <summary>Kartı çevirir.</summary>
    public FlipResult Flip(int index)
    {
        if (index < 0 || index >= _cards.Count)
        {
            return FlipResult.Ignored;
        }

        if (_matched.Contains(index) || _faceUp.Contains(index))
        {
            return FlipResult.Ignored;
        }

        // İki kart zaten açıksa yeni çevirme kabul edilmez; önce kapanmalı.
        if (_faceUp.Count >= 2)
        {
            return FlipResult.Ignored;
        }

        _faceUp.Add(index);
        if (_faceUp.Count < 2)
        {
            return FlipResult.AwaitingSecond;
        }

        if (_cards[_faceUp[0]].SymbolId == _cards[_faceUp[1]].SymbolId)
        {
            _matched.UnionWith(_faceUp);
            _faceUp.Clear();
            return FlipResult.Matched;
        }

        // Filiz bandında hata sayılmıyor: o yaşta kartı yanlış açmak oyunun
        // kendisi, hata değil. Yıldız hesabına da girmiyor.
        if (Band != AgeBand.Filiz)
        {
            Mistakes++;
        }

        return FlipResult.Mismatched;
    }

    /// <summary>Eşleşmeyen çifti kapatır.</summary>
    public void CloseMismatch()
    {
        if (_faceUp.Count >= 2)
        {
            _faceUp.Clear();
        }
    }

    private static void Shuffle<T>(IList<T> items, Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }
}
