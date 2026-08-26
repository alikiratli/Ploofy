using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Bir parçayı yuvaya bırakmanın sonucu.</summary>
public enum PlaceOutcome
{
    /// <summary>Bırakma yok sayıldı (yuva dışına bırakıldı, tur bitmiş).</summary>
    Ignored,

    /// <summary>Parça yerine oturdu.</summary>
    Fitted,

    /// <summary>Yanlış yuva — parça tepside kalıyor.</summary>
    WrongSlot,
}

/// <summary>
/// Yapbozun tek parçası.
/// </summary>
/// <remarks>
/// <para>
/// Kenar tırnakları burada duruyor: <c>0</c> düz kenar (yapbozun dış
/// çerçevesi), <c>+1</c> dışarı çıkan tırnak, <c>-1</c> içeri giren yuva.
/// Komşu iki parçanın ortak kenarı her zaman birbirinin tersi — parçanın
/// çizimi de yuvasının çizimi de aynı sayılardan türediği için ekranda
/// birebir oturuyorlar.
/// </para>
/// <para>
/// Tırnaklar arayüzde değil burada, çünkü kesim <b>turun bir parçası</b>:
/// aynı tohumla kurulan tur aynı yapbozu veriyor ve testler komşuların
/// gerçekten geçtiğini doğrulayabiliyor.
/// </para>
/// </remarks>
public sealed class JigsawPiece
{
    public int Id { get; init; }

    public int Row { get; init; }

    public int Column { get; init; }

    public int Top { get; internal set; }

    public int Right { get; internal set; }

    public int Bottom { get; internal set; }

    public int Left { get; internal set; }

    public bool IsPlaced { get; internal set; }
}

/// <summary>Yapbozun banda göre zorluk tablosu.</summary>
public static class JigsawTuning
{
    /// <summary>Izgaranın bir kenarındaki parça sayısı — yapboz her zaman kare.</summary>
    /// <remarks>
    /// 4, 9 ve 16 parça. Meşe'nin on altısı uzun bir tur ama yapbozda
    /// asıl ödül tablonun tamamlanması; sekiz parçada tablo daha bitmeden
    /// bitiyor ve o his oluşmuyor.
    /// </remarks>
    public static readonly BandValue<int> Grid = new(2, 3, 4);

    /// <summary>
    /// Boş yuvaların altında resmin soluk bir kopyası duruyor mu?
    /// </summary>
    /// <remarks>
    /// Bandın asıl farkı bu. Hayalet varken oyun "resmi eşleştir": çocuk
    /// parçadaki deseni yuvadakiyle karşılaştırıyor. Meşe'de hayalet yok ve
    /// oyun "resmi kur"a dönüşüyor — parçanın nereye gideceği ancak
    /// yerleşmiş komşulara bakarak çıkarılıyor. Aynı yapboz, iki farklı iş.
    /// </remarks>
    public static readonly BandValue<bool> ShowsGhost = new(true, true, false);

    /// <summary>
    /// Bırakmanın yuvaya sayılması için gereken yakınlık (hücrenin yarısına oranla).
    /// </summary>
    /// <remarks>
    /// Parçayı yuvanın tam ortasına bırakmak bu yaşta beklenemez. Filiz'de
    /// alan hücrenin tamamını aşıyor, yani tahtaya bırakılan parça en yakın
    /// yuvaya gidiyor; Meşe'de gerçekten nişan almak gerekiyor.
    /// </remarks>
    public static readonly BandValue<float> SnapReach = new(1.4f, 1.0f, 0.7f);

    /// <summary>Üçüncü yıldız için hedef süre (yalnızca Meşe).</summary>
    /// <remarks>
    /// Kütüphanedeki son üç oyunun aksine burada süre anlamlı: tahtanın
    /// tamamı en baştan görünüyor ve hızlı bitirmek gerçekten "daha çabuk
    /// çözdüm" demek. Bekleyecek bir gösterim, düşecek bir nesne yok.
    /// </remarks>
    public static readonly BandValue<TimeSpan?> ParTime = new(
        null,
        null,
        TimeSpan.FromSeconds(150));
}

/// <summary>
/// Yapbozun bir turu — çizimden bağımsız kurallar.
/// </summary>
/// <remarks>
/// <para>
/// Tahtada boş yuvalar duruyor, altta sıradaki parça bekliyor; çocuk parçayı
/// yuvasına sürüklüyor. Parçalar Şekil Ayırma'daki gibi <b>sırayla</b>
/// geliyor: on altı parçayı aynı anda ekrana dökmek bu yaş grubunda
/// dağıtıyor ve "şimdi ne yapacağım" sorusunu geri getiriyor.
/// </para>
/// <para>
/// Resmin kendisi burada yok. Motor yalnızca <see cref="PictureSeed"/>
/// veriyor, resmi arayüz o tohumdan üretiyor — hangi renklerin nereye
/// düştüğü çizim katmanının işi, ama aynı turun aynı resmi vermesi motorun
/// güvencesi.
/// </para>
/// </remarks>
public sealed class JigsawRound
{
    private readonly Random _rng;
    private readonly JigsawPiece[] _pieces;
    private readonly List<JigsawPiece> _order;

    private JigsawRound(AgeBand band, Random rng)
    {
        Band = band;
        _rng = rng;

        Grid = JigsawTuning.Grid.For(band);
        ShowsGhost = JigsawTuning.ShowsGhost.For(band);
        SnapReach = JigsawTuning.SnapReach.For(band);
        ParTime = JigsawTuning.ParTime.For(band);
        PictureSeed = rng.Next();

        _pieces = BuildPieces();
        _order = BuildOrder();
    }

    public static JigsawRound ForBand(AgeBand band, Random? random = null) =>
        new(band, random ?? Random.Shared);

    public AgeBand Band { get; }

    /// <summary>Izgaranın bir kenarındaki parça sayısı.</summary>
    public int Grid { get; }

    public bool ShowsGhost { get; }

    public float SnapReach { get; }

    public TimeSpan? ParTime { get; }

    /// <summary>Arayüzün resmi ürettiği tohum.</summary>
    public int PictureSeed { get; }

    /// <summary>Bütün parçalar, satır satır.</summary>
    public IReadOnlyList<JigsawPiece> Pieces => _pieces;

    /// <summary>Henüz yerleşmemiş parçalar, geliş sırasıyla.</summary>
    public IReadOnlyList<JigsawPiece> Tray => _order;

    public int Total => _pieces.Length;

    public int Placed { get; private set; }

    public int Mistakes { get; private set; }

    /// <summary>Sıradaki parça; tur bittiyse null.</summary>
    public JigsawPiece? Current => _order.Count > 0 ? _order[0] : null;

    /// <summary>Ondan sonraki parça — arayüz onu arkada soluk gösteriyor.</summary>
    public JigsawPiece? Next => _order.Count > 1 ? _order[1] : null;

    public bool IsComplete => Placed >= Total;

    public JigsawPiece PieceAt(int row, int column) => _pieces[(row * Grid) + column];

    /// <summary>
    /// Sıradaki parçayı bir yuvaya bırakır.
    /// </summary>
    /// <remarks>
    /// Yanlış yuvada parça <b>kaybolmuyor</b>, tepsinin başında kalıyor:
    /// çocuk doğru yeri bulana kadar deneyebiliyor. Filiz bandında bu hiç
    /// hata sayılmıyor — Şekil Ayırma'daki kararın aynısı.
    /// </remarks>
    public PlaceOutcome Place(int row, int column)
    {
        if (Current is not { } piece)
        {
            return PlaceOutcome.Ignored;
        }

        if (row < 0 || row >= Grid || column < 0 || column >= Grid)
        {
            return PlaceOutcome.Ignored;
        }

        if (row != piece.Row || column != piece.Column)
        {
            if (Band != AgeBand.Filiz)
            {
                Mistakes++;
            }

            return PlaceOutcome.WrongSlot;
        }

        piece.IsPlaced = true;
        _order.RemoveAt(0);
        Placed++;
        return PlaceOutcome.Fitted;
    }

    /// <summary>Izgarayı kurar ve komşu kenarları birbirinin tersi yapar.</summary>
    private JigsawPiece[] BuildPieces()
    {
        var pieces = new JigsawPiece[Grid * Grid];

        for (var row = 0; row < Grid; row++)
        {
            for (var column = 0; column < Grid; column++)
            {
                pieces[(row * Grid) + column] = new JigsawPiece
                {
                    Id = (row * Grid) + column,
                    Row = row,
                    Column = column,
                };
            }
        }

        // Dikey kesimler: sağdaki parçanın sol kenarı, soldakinin sağ
        // kenarının tersi. Dış çerçeve 0 kalıyor.
        for (var row = 0; row < Grid; row++)
        {
            for (var column = 0; column < Grid - 1; column++)
            {
                var tab = _rng.Next(2) == 0 ? 1 : -1;
                pieces[(row * Grid) + column].Right = tab;
                pieces[(row * Grid) + column + 1].Left = -tab;
            }
        }

        // Yatay kesimler.
        for (var row = 0; row < Grid - 1; row++)
        {
            for (var column = 0; column < Grid; column++)
            {
                var tab = _rng.Next(2) == 0 ? 1 : -1;
                pieces[(row * Grid) + column].Bottom = tab;
                pieces[((row + 1) * Grid) + column].Top = -tab;
            }
        }

        return pieces;
    }

    /// <summary>
    /// Parçaların geliş sırası.
    /// </summary>
    /// <remarks>
    /// Hayaletin olmadığı bantta sıra rastgele <b>olamaz</b>: ortadan gelen
    /// yalnız bir parçanın nereye gideceğini çıkarmanın yolu yok, çünkü
    /// etrafında bakılacak hiçbir şey yok. Orada sıra köşeden başlıyor ve
    /// her parça yerleşmişlerden en az birine komşu geliyor — yapboz zor
    /// kalıyor ama çözülebilir oluyor.
    /// </remarks>
    private List<JigsawPiece> BuildOrder()
    {
        if (ShowsGhost)
        {
            return [.. _pieces.OrderBy(_ => _rng.Next())];
        }

        var remaining = _pieces.ToList();
        var order = new List<JigsawPiece>(remaining.Count);

        // Köşeden başlamak, ilk parçayı da bir bilmece olmaktan çıkarıyor:
        // düz iki kenarı olan parça tahtanın neresine gittiğini kendi söylüyor.
        var corners = remaining
            .Where(p => (p.Row == 0 || p.Row == Grid - 1) &&
                        (p.Column == 0 || p.Column == Grid - 1))
            .ToList();

        var first = corners[_rng.Next(corners.Count)];
        order.Add(first);
        remaining.Remove(first);

        while (remaining.Count > 0)
        {
            var reachable = remaining
                .Where(candidate => order.Any(placed => AreNeighbours(placed, candidate)))
                .ToList();

            // Izgara bağlı olduğu için burası hiç boşalmıyor; yine de
            // kalanlardan seçmek, kuralın bir gün değişmesi hâlinde turun
            // yarıda kalmasını engelliyor.
            var pool = reachable.Count > 0 ? reachable : remaining;
            var next = pool[_rng.Next(pool.Count)];

            order.Add(next);
            remaining.Remove(next);
        }

        return order;
    }

    private static bool AreNeighbours(JigsawPiece a, JigsawPiece b) =>
        Math.Abs(a.Row - b.Row) + Math.Abs(a.Column - b.Column) == 1;
}
