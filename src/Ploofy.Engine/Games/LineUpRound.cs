using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Parçaların neye göre sıralandığı.</summary>
public enum SortAttribute
{
    /// <summary>Boyuta göre. Saymak gerekmiyor.</summary>
    Size,

    /// <summary>Miktara göre. Saymak gerekiyor.</summary>
    Quantity,
}

/// <summary>Sıralama yönü.</summary>
public enum SortDirection
{
    /// <summary>Küçükten büyüğe, azdan çoğa.</summary>
    Ascending,

    /// <summary>Büyükten küçüğe, çoktan aza.</summary>
    Descending,
}

/// <summary>
/// Sıralanacak tek parça.
/// </summary>
/// <remarks>
/// Bırakmanın sonucu Yapboz'un <see cref="PlaceOutcome"/>'ıyla anlatılıyor:
/// üç durumu da (yok sayıldı, yerine oturdu, yanlış yuva) birebir aynı ve
/// ikinci bir aynı anlamlı sıralama türü kafa karıştırır.
/// </remarks>
/// <param name="Count">Kaç nesne taşıdığı. Boyuta göre sıralamada her zaman 1.</param>
/// <param name="Size">
/// Nesnenin göreli boyutu (0-1). Miktara göre sıralamada hepsi aynı — tek
/// seferde tek bir şey değişsin diye.
/// </param>
/// <param name="Rank">Doğru sırası (0 tabanlı). Arayüz bunu bilmiyor, motor karar veriyor.</param>
public sealed record LineUpPiece(
    int Id,
    ShapeKind Kind,
    BubbleHue Hue,
    int Count,
    float Size,
    int Rank);

/// <summary>Sırala'nın banda göre zorluk tablosu.</summary>
public static class LineUpTuning
{
    /// <summary>Bir turda kaç bulmaca çözülüyor.</summary>
    public static readonly BandValue<int> Puzzles = new(3, 4, 5);

    /// <summary>Bir bulmacada kaç parça sıralanıyor.</summary>
    /// <remarks>
    /// İkiden başlamıyor: iki parçayı sıralamak "hangisi daha büyük" sorusunun
    /// kendisi ve Filiz bandında bile üç parça, ortadakini bulmayı — yani
    /// sıralamanın asıl fikrini — gerektiriyor.
    /// </remarks>
    public static readonly BandValue<int> Pieces = new(3, 4, 5);

    /// <summary>
    /// Neye göre sıralanacağı.
    /// </summary>
    /// <remarks>
    /// Filiz boyuta bakıyor: iki yaşındaki çocuk saymıyor ama büyüğü küçükten
    /// ayırıyor. Fidan'dan itibaren miktara geçiyor — bu, Say ve Eşleştir'in
    /// öğrettiği saymanın bir sonraki adımı: sayıları birbiriyle kıyaslamak.
    /// </remarks>
    public static readonly BandValue<SortAttribute> Attribute = new(
        SortAttribute.Size,
        SortAttribute.Quantity,
        SortAttribute.Quantity);

    /// <summary>
    /// İki komşu miktar arasındaki en küçük fark.
    /// </summary>
    /// <remarks>
    /// Fidan'da miktarlar birbirinden uzak (1, 3, 5, 7): fark bakışla
    /// görülüyor, saymak şart değil. Meşe'de ardışık (4, 5, 6, 7, 8) —
    /// orada gerçekten saymak gerekiyor, oyunun öğretici tarafı da o.
    /// Boyuta göre sıralamada kullanılmıyor.
    /// </remarks>
    public static readonly BandValue<int> QuantityGap = new(2, 2, 1);

    /// <summary>En büyük miktar bu sayıyı geçmiyor.</summary>
    public static readonly BandValue<int> MaxQuantity = new(3, 8, 12);

    /// <summary>
    /// Yön değişebiliyor mu?
    /// </summary>
    /// <remarks>
    /// Yalnızca Meşe'de. "Çoktan aza sırala" bir sonraki bilişsel adım: çocuk
    /// artık sıralamayı ezberlenmiş bir hareket olarak değil, verilen bir
    /// kurala göre yapıyor. Küçük bantlarda hep azdan çoğa — değişen yön o
    /// yaşta öğretmiyor, şaşırtıyor.
    /// </remarks>
    public static readonly BandValue<bool> DirectionVaries = new(false, false, true);

    /// <summary>Yıldız için hedef süre; küçük bantlarda süre yok.</summary>
    public static readonly BandValue<TimeSpan?> ParTime = new(
        null,
        null,
        TimeSpan.FromSeconds(90));
}

/// <summary>
/// Sırala turu: parçalar boyuta ya da miktara göre diziliyor.
/// </summary>
/// <remarks>
/// <para>
/// Say ve Eşleştir miktarı rakama bağlıyor; burası miktarları <b>birbiriyle</b>
/// kıyaslıyor. Az/çok ve küçük/büyük, sayı doğrusunun okul öncesindeki
/// karşılığı ve toplamaya zemin hazırlayan beceri.
/// </para>
/// <para>
/// Her bulmacada <b>tek bir şey</b> değişiyor: boyuta göre sıralarken bütün
/// parçalar aynı şekil ve renkte, miktara göre sıralarken hepsi aynı boyutta.
/// İki boyutta birden değişen bir dizi iki ayrı bilmece demek — Örüntü'deki
/// karar burada da geçerli.
/// </para>
/// <para>
/// Parça herhangi bir yuvaya bırakılabiliyor; yalnızca doğru yuva kabul
/// ediyor. Soldan sağa doldurmayı dayatmak, en büyüğü ilk gören çocuğa
/// "önce en küçüğü bul" demek olurdu — sıralamanın tek doğru yolu yok.
/// </para>
/// <para>
/// Bulmaca çözülünce motor <b>kendiliğinden</b> sonrakine geçmiyor:
/// <see cref="NextPuzzle"/> çağrılana kadar tamamlanmış dizi ekranda
/// duruyor. Motor "nerede kalındı"yı bilir, "ne zaman"ı bilmez — kutlamanın
/// ne kadar süreceği arayüzün kararı.
/// </para>
/// </remarks>
public sealed class LineUpRound
{
    private static readonly ShapeKind[] Kinds = Enum.GetValues<ShapeKind>();
    private static readonly BubbleHue[] Hues = Enum.GetValues<BubbleHue>();

    /// <summary>Boyuta göre sıralamada en küçük ve en büyük parça.</summary>
    /// <remarks>
    /// Aralık geniş tutuldu: en küçük parça en büyüğün üçte biri, yani fark
    /// yan yana durmadan da görülüyor. Dar bir aralık, boyut sıralamasını
    /// göz testine çeviriyor.
    /// </remarks>
    private const float SmallestSize = 0.34f;
    private const float LargestSize = 1f;

    private readonly Random _rng;
    private readonly int _pieceCount;
    private readonly SortAttribute _attribute;
    private readonly int _quantityGap;
    private readonly int _maxQuantity;
    private readonly bool _directionVaries;

    private readonly List<LineUpPiece?> _slots = [];
    private readonly List<LineUpPiece> _tray = [];

    private int _nextId;

    private LineUpRound(AgeBand band, Random rng)
    {
        Band = band;
        _rng = rng;

        Total = LineUpTuning.Puzzles.For(band);
        _pieceCount = LineUpTuning.Pieces.For(band);
        _attribute = LineUpTuning.Attribute.For(band);
        _quantityGap = LineUpTuning.QuantityGap.For(band);
        _maxQuantity = LineUpTuning.MaxQuantity.For(band);
        _directionVaries = LineUpTuning.DirectionVaries.For(band);

        ParTime = LineUpTuning.ParTime.For(band);

        BuildPuzzle();
    }

    /// <summary>Bant için standart bir tur kurar. <paramref name="random"/> testlerde sabitlenebilir.</summary>
    public static LineUpRound ForBand(AgeBand band, Random? random = null) =>
        new(band, random ?? Random.Shared);

    public AgeBand Band { get; }

    public TimeSpan? ParTime { get; }

    /// <summary>Turdaki bulmaca sayısı.</summary>
    public int Total { get; }

    /// <summary>Çözülmüş bulmaca sayısı.</summary>
    public int Completed { get; private set; }

    public int Mistakes { get; private set; }

    /// <summary>Bu bulmacada neye göre sıralanıyor.</summary>
    public SortAttribute Attribute => _attribute;

    /// <summary>Bu bulmacanın yönü. Arayüz oku buna göre çiziyor.</summary>
    public SortDirection Direction { get; private set; }

    /// <summary>Henüz yerleştirilmemiş parçalar, karışık sırada.</summary>
    public IReadOnlyList<LineUpPiece> Tray => _tray;

    /// <summary>Yuvalar, soldan sağa. Boş olanlar <c>null</c>.</summary>
    public IReadOnlyList<LineUpPiece?> Slots => _slots;

    /// <summary>Bu bulmaca bitti mi?</summary>
    public bool PuzzleSolved => _tray.Count == 0;

    public bool IsComplete => Completed >= Total;

    /// <summary>
    /// Parçayı yuvaya bırakır.
    /// </summary>
    /// <remarks>
    /// Yanlış yuvada parça tepsiye dönüyor ve bulmaca duruyor: doğruyu
    /// görmeden geçmek, oyunun öğretici olma iddiasını boşa çıkarırdı.
    /// Filiz bandında hata sayılmıyor.
    /// </remarks>
    public PlaceOutcome Place(int pieceId, int slotIndex)
    {
        if (IsComplete || slotIndex < 0 || slotIndex >= _slots.Count)
        {
            return PlaceOutcome.Ignored;
        }

        if (_slots[slotIndex] is not null)
        {
            return PlaceOutcome.Ignored;
        }

        var piece = _tray.FirstOrDefault(p => p.Id == pieceId);
        if (piece is null)
        {
            return PlaceOutcome.Ignored;
        }

        if (piece.Rank != slotIndex)
        {
            if (Band != AgeBand.Filiz)
            {
                Mistakes++;
            }

            return PlaceOutcome.WrongSlot;
        }

        _slots[slotIndex] = piece;
        _tray.Remove(piece);

        if (PuzzleSolved)
        {
            Completed++;
        }

        return PlaceOutcome.Fitted;
    }

    /// <summary>
    /// Sıradaki bulmacayı kurar.
    /// </summary>
    /// <remarks>
    /// Arayüz, tamamlanmış diziyi gösterdikten sonra çağırıyor. Çözülmemiş
    /// bir bulmacada ya da tur bittikten sonra hiçbir şey yapmıyor: yarım
    /// bırakılmış bir diziyi kazayla silmek, çocuğun yaptığı işi yok etmek
    /// olurdu.
    /// </remarks>
    public void NextPuzzle()
    {
        if (!PuzzleSolved || IsComplete)
        {
            return;
        }

        BuildPuzzle();
    }

    private void BuildPuzzle()
    {
        Direction = _directionVaries && _rng.Next(2) == 0
            ? SortDirection.Descending
            : SortDirection.Ascending;

        // Tek şekil, tek renk: sıralanan özellik dışında hiçbir şey
        // değişmiyor.
        var kind = Kinds[_rng.Next(Kinds.Length)];
        var hue = Hues[_rng.Next(Hues.Length)];

        var pieces = _attribute == SortAttribute.Size
            ? BuildBySize(kind, hue)
            : BuildByQuantity(kind, hue);

        _slots.Clear();
        for (var i = 0; i < _pieceCount; i++)
        {
            _slots.Add(null);
        }

        _tray.Clear();
        _tray.AddRange(pieces.OrderBy(_ => _rng.Next()));
    }

    private List<LineUpPiece> BuildBySize(ShapeKind kind, BubbleHue hue)
    {
        var pieces = new List<LineUpPiece>(_pieceCount);

        for (var i = 0; i < _pieceCount; i++)
        {
            var t = i / (float)(_pieceCount - 1);
            var size = SmallestSize + ((LargestSize - SmallestSize) * t);

            pieces.Add(new LineUpPiece(
                _nextId++, kind, hue, Count: 1, size, RankFor(i)));
        }

        return pieces;
    }

    private List<LineUpPiece> BuildByQuantity(ShapeKind kind, BubbleHue hue)
    {
        // En küçük miktar, en büyüğü sınırı aşmayacak kadar aşağıdan
        // başlıyor. Bir taban kaydırması, her bulmacanın hep 1'den
        // başlamamasını sağlıyor.
        var span = _quantityGap * (_pieceCount - 1);
        var highestStart = Math.Max(1, _maxQuantity - span);
        var start = _rng.Next(1, highestStart + 1);

        var pieces = new List<LineUpPiece>(_pieceCount);

        for (var i = 0; i < _pieceCount; i++)
        {
            pieces.Add(new LineUpPiece(
                _nextId++,
                kind,
                hue,
                Count: start + (i * _quantityGap),
                Size: 1f,
                RankFor(i)));
        }

        return pieces;
    }

    /// <summary>
    /// Küçükten büyüğe kurulan listedeki <paramref name="index"/> parçasının
    /// yuvası.
    /// </summary>
    /// <remarks>
    /// Yön tersse yuva da tersleniyor. Parçaların kendisini ters kurmak yerine
    /// yalnızca sırayı çevirmek, iki yolun aynı parçaları üretmesini garanti
    /// ediyor — "çoktan aza" bulmacası "azdan çoğa"nın aynadaki hâli.
    /// </remarks>
    private int RankFor(int index) =>
        Direction == SortDirection.Ascending ? index : _pieceCount - 1 - index;
}
