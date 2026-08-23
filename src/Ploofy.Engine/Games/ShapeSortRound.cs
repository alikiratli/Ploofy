using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Ayrılacak şekil türü.</summary>
public enum ShapeKind
{
    Circle,
    Square,
    Triangle,
    Star,
    Heart,
    Hexagon,
}

/// <summary>Bir parçayı kutuya bırakmanın sonucu.</summary>
public enum DropOutcome
{
    /// <summary>Bırakma yok sayıldı (parça zaten yerleşmiş, tur bitmiş).</summary>
    Ignored,

    /// <summary>Doğru kutu.</summary>
    Sorted,

    /// <summary>Yanlış kutu — parça yerine dönüyor.</summary>
    WrongBin,
}

/// <summary>Ayrılacak tek parça.</summary>
/// <param name="Hue">
/// Rengin ayırmayla ilgisi yok, yalnızca görsel çeşitlilik — hangi bantta
/// renk ipucu verdiğini <see cref="ShapeSortRound.ColorMatchesShape"/> söylüyor.
/// </param>
public sealed record ShapePiece(int Id, ShapeKind Kind, BubbleHue Hue);

/// <summary>Şekil Ayırma'nın banda göre zorluk tablosu.</summary>
public static class ShapeSortTuning
{
    /// <summary>Kaç kutu (yani kaç farklı şekil) olacağı.</summary>
    public static readonly BandValue<int> BinCount = new(2, 3, 4);

    /// <summary>Ayrılacak toplam parça sayısı.</summary>
    public static readonly BandValue<int> PieceCount = new(6, 9, 12);

    /// <summary>
    /// Rengin şekille birlikte gitmesi (her şeklin kendi rengi olması).
    /// </summary>
    /// <remarks>
    /// Bandın asıl farkı bu. Filiz'de renk ve şekil aynı şeyi söylüyor:
    /// çocuk hangisine bakarsa baksın doğru kutuyu buluyor. Fidan'dan
    /// itibaren renkler şekillerden bağımsız dağıtılıyor, yani artık
    /// gerçekten şekle bakmak gerekiyor. Aynı oyun, iki farklı beceri.
    /// </remarks>
    public static readonly BandValue<bool> ColorMatchesShape = new(true, false, false);

    /// <summary>Üçüncü yıldız için hedef süre (yalnızca Meşe).</summary>
    public static readonly BandValue<TimeSpan?> ParTime = new(
        null,
        null,
        TimeSpan.FromSeconds(50));
}

/// <summary>
/// Şekil Ayırma oyununun bir turu — arayüzden bağımsız kurallar.
/// </summary>
/// <remarks>
/// Parçalar sırayla geliyor: ekranda tek bir parça duruyor ve çocuk onu
/// kutulardan birine bırakıyor. Hepsini aynı anda göstermek bu yaş grubunda
/// dağıtıyor; sıradaki tek parça "şimdi ne yapacağım" sorusunu ortadan
/// kaldırıyor.
/// </remarks>
public sealed class ShapeSortRound
{
    private readonly List<ShapePiece> _queue;

    private ShapeSortRound(AgeBand band, List<ShapeKind> bins, List<ShapePiece> queue)
    {
        Band = band;
        Bins = bins;
        _queue = queue;
        Total = queue.Count;
        ParTime = ShapeSortTuning.ParTime.For(band);
        ColorMatchesShape = ShapeSortTuning.ColorMatchesShape.For(band);
    }

    /// <summary>Bant için standart bir tur kurar. <paramref name="random"/> testlerde sabitlenebilir.</summary>
    public static ShapeSortRound ForBand(AgeBand band, Random? random = null)
    {
        var rng = random ?? Random.Shared;

        var binCount = ShapeSortTuning.BinCount.For(band);
        var pieceCount = ShapeSortTuning.PieceCount.For(band);
        var colorMatches = ShapeSortTuning.ColorMatchesShape.For(band);

        var bins = Enum.GetValues<ShapeKind>()
            .OrderBy(_ => rng.Next())
            .Take(binCount)
            .ToList();

        var hues = Enum.GetValues<BubbleHue>().OrderBy(_ => rng.Next()).ToList();

        // Her kutuya eşit sayıda parça. Eşitlik önemli: bir şekilden tek
        // parça gelirse o kutu turun sonuna kadar boş duruyor ve çocuk
        // kutunun bozuk olduğunu sanıyor.
        var perBin = pieceCount / binCount;
        var pieces = new List<ShapePiece>(perBin * binCount);
        var id = 0;

        for (var b = 0; b < bins.Count; b++)
        {
            for (var i = 0; i < perBin; i++)
            {
                var hue = colorMatches ? hues[b] : hues[rng.Next(hues.Count)];
                pieces.Add(new ShapePiece(id++, bins[b], hue));
            }
        }

        Shuffle(pieces, rng);

        // Aynı şekil üst üste üç kez gelmesin: sıra rastgele olduğu hâlde
        // çocuk "hep aynı kutu" hissine kapılmasın.
        SpreadOutRuns(pieces);

        return new ShapeSortRound(band, bins, pieces);
    }

    public AgeBand Band { get; }

    /// <summary>Ekrandaki kutular, soldan sağa.</summary>
    public IReadOnlyList<ShapeKind> Bins { get; }

    /// <summary>Bu bantta renk şekille aynı şeyi söylüyor mu?</summary>
    public bool ColorMatchesShape { get; }

    public TimeSpan? ParTime { get; }

    public int Total { get; }

    public int Sorted { get; private set; }

    public int Mistakes { get; private set; }

    public int Remaining => _queue.Count;

    public bool IsComplete => _queue.Count == 0;

    /// <summary>Sıradaki parça; tur bittiyse null.</summary>
    public ShapePiece? Current => _queue.Count > 0 ? _queue[0] : null;

    /// <summary>Sıradan sonraki parça — arayüz onu arkada soluk gösteriyor.</summary>
    public ShapePiece? Next => _queue.Count > 1 ? _queue[1] : null;

    /// <summary>
    /// Sıradaki parçayı bir kutuya bırakır.
    /// </summary>
    /// <remarks>
    /// Yanlış kutuda parça <b>kaybolmuyor</b>, sıranın başında kalıyor:
    /// çocuk aynı parçayı doğru kutuya koyana kadar deneyebiliyor. Filiz
    /// bandında bu hiç hata sayılmıyor — o bantta yanlış kutu denemek
    /// öğrenmenin kendisi.
    /// </remarks>
    public DropOutcome Drop(ShapeKind bin)
    {
        if (Current is not { } piece)
        {
            return DropOutcome.Ignored;
        }

        if (!Bins.Contains(bin))
        {
            return DropOutcome.Ignored;
        }

        if (piece.Kind != bin)
        {
            if (Band != AgeBand.Filiz)
            {
                Mistakes++;
            }

            return DropOutcome.WrongBin;
        }

        _queue.RemoveAt(0);
        Sorted++;
        return DropOutcome.Sorted;
    }

    private static void Shuffle<T>(IList<T> items, Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>
    /// Üç ve daha uzun aynı-şekil dizilerini böler.
    /// </summary>
    private static void SpreadOutRuns(List<ShapePiece> pieces)
    {
        for (var i = 2; i < pieces.Count; i++)
        {
            if (pieces[i].Kind != pieces[i - 1].Kind || pieces[i].Kind != pieces[i - 2].Kind)
            {
                continue;
            }

            // İleride farklı şekilli ilk parçayla yer değiştir.
            for (var j = i + 1; j < pieces.Count; j++)
            {
                if (pieces[j].Kind == pieces[i].Kind)
                {
                    continue;
                }

                (pieces[i], pieces[j]) = (pieces[j], pieces[i]);
                break;
            }
        }
    }
}
