using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Bir seçeneğe dokunmanın sonucu.</summary>
public enum PatternOutcome
{
    /// <summary>Dokunuş yok sayıldı (tur bitmiş ya da tanınmayan seçenek).</summary>
    Ignored,

    /// <summary>Doğru parça.</summary>
    Correct,

    /// <summary>Yanlış parça — dizi değişmiyor, çocuk yeniden bakabiliyor.</summary>
    Wrong,
}

/// <summary>Örüntüdeki tek parça.</summary>
public readonly record struct PatternTile(ShapeKind Kind, BubbleHue Hue);

/// <summary>Boşluğa konabilecek bir seçenek.</summary>
public sealed record PatternChoice(int Id, PatternTile Tile, bool IsCorrect);

/// <summary>
/// Ekrandaki dizi ve altındaki seçenekler.
/// </summary>
/// <param name="Sequence">
/// Soldan sağa parçalar; <c>null</c> olan tek eleman boşluk.
/// </param>
public sealed record PatternQuestion(
    IReadOnlyList<PatternTile?> Sequence,
    int BlankIndex,
    IReadOnlyList<PatternChoice> Choices);

/// <summary>Örüntü Tamamlama'nın banda göre zorluk tablosu.</summary>
public static class PatternTuning
{
    /// <summary>Turdaki soru sayısı.</summary>
    public static readonly BandValue<int> Questions = new(4, 6, 8);

    /// <summary>Altta kaç seçenek duracağı.</summary>
    public static readonly BandValue<int> Choices = new(2, 3, 4);

    /// <summary>Ekrandaki dizinin uzunluğu, boşluk dahil.</summary>
    /// <remarks>
    /// Yatay ekranda yan yana dizildikleri için üst sınır ekranın kendisi.
    /// Dokuz parça, dokunma hedefini 64 birimin altına düşürmeden sığan en
    /// büyük sayı.
    /// </remarks>
    public static readonly BandValue<int> Length = new(6, 8, 9);

    /// <summary>
    /// İzin verilen örüntü birimleri.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Harfler soyut: A, B, C aynı turda birbirinden farklı üç parçaya
    /// karşılık geliyor. Sıra da zorluk sırası — AB en basit örüntü,
    /// AABB'yi görmek için önce AAB'yi çözebilmek gerekiyor.
    /// </para>
    /// <para>
    /// Filiz yalnızca AB görüyor. Bu bantta amaç örüntü kurmak değil,
    /// "bir şey tekrar ediyor" fikrini yakalamak.
    /// </para>
    /// </remarks>
    public static readonly BandValue<string[]> Units = new(
        ["AB"],
        ["AB", "AAB", "ABB"],
        ["AB", "AAB", "ABB", "ABC", "AABB"]);

    /// <summary>
    /// Parçalar şekilce de farklı mı?
    /// </summary>
    /// <remarks>
    /// Filiz'de bütün parçalar aynı şekil, yalnızca renkleri değişiyor: iki
    /// boyutta birden değişen bir dizi, örüntüyü henüz kavramamış bir çocuk
    /// için iki ayrı bilmece demek.
    /// </remarks>
    public static readonly BandValue<bool> VariesShape = new(false, true, true);

    /// <summary>
    /// Boşluk dizinin sonunda mı, herhangi bir yerinde mi?
    /// </summary>
    /// <remarks>
    /// Sondaki boşluk "sırada ne var" sorusu; ortadaki boşluk "burada ne
    /// eksik" sorusu. İkincisi belirgin biçimde zor, çünkü çocuğun sağdaki
    /// parçaları da hesaba katması gerekiyor.
    /// </remarks>
    public static readonly BandValue<bool> BlankCanBeInside = new(false, false, true);

    /// <summary>Yıldız için hedef süre; küçük bantlarda süre yok.</summary>
    public static readonly BandValue<TimeSpan?> ParTime = new(
        null,
        null,
        TimeSpan.FromSeconds(60));
}

/// <summary>
/// Örüntü Tamamlama turu.
/// </summary>
/// <remarks>
/// <para>
/// Ekranda tekrar eden bir dizi duruyor, bir parçası eksik; çocuk alttaki
/// seçeneklerden doğru olanı seçiyor. Okul öncesi matematiğin belkemiği:
/// örüntü görmek, sayı saymaktan önce gelen ve toplamaya zemin hazırlayan
/// beceri.
/// </para>
/// <para>
/// Yanlış seçimde dizi <b>değişmiyor</b>: çocuk yeniden bakıp bir daha
/// deneyebiliyor. Doğruyu görmeden geçmek, oyunun öğretici olma iddiasını
/// boşa çıkarırdı — Say ve Eşleştir ile Harf Avı'ndaki karar burada da
/// geçerli.
/// </para>
/// </remarks>
public sealed class PatternRound
{
    private static readonly ShapeKind[] Kinds = Enum.GetValues<ShapeKind>();
    private static readonly BubbleHue[] Hues = Enum.GetValues<BubbleHue>();

    private readonly Random _rng;
    private readonly string[] _units;
    private readonly int _length;
    private readonly int _choiceCount;
    private readonly bool _variesShape;
    private readonly bool _blankCanBeInside;

    /// <summary>Aynı birimin arka arkaya gelmemesi için son kullanılan.</summary>
    private string _lastUnit = string.Empty;

    private int _nextChoiceId;

    private PatternRound(AgeBand band, Random rng)
    {
        Band = band;
        _rng = rng;

        Total = PatternTuning.Questions.For(band);
        _units = PatternTuning.Units.For(band);
        _length = PatternTuning.Length.For(band);
        _choiceCount = PatternTuning.Choices.For(band);
        _variesShape = PatternTuning.VariesShape.For(band);
        _blankCanBeInside = PatternTuning.BlankCanBeInside.For(band);

        ParTime = PatternTuning.ParTime.For(band);

        Current = BuildQuestion();
    }

    /// <summary>Bant için standart bir tur kurar. <paramref name="random"/> testlerde sabitlenebilir.</summary>
    public static PatternRound ForBand(AgeBand band, Random? random = null) =>
        new(band, random ?? Random.Shared);

    public AgeBand Band { get; }

    public TimeSpan? ParTime { get; }

    /// <summary>Turdaki toplam soru sayısı.</summary>
    public int Total { get; }

    public int Correct { get; private set; }

    public int Mistakes { get; private set; }

    /// <summary>Sıradaki soru; tur bittiyse null.</summary>
    public PatternQuestion? Current { get; private set; }

    public bool IsComplete => Correct >= Total;

    /// <summary>
    /// Bir seçeneğe dokunur.
    /// </summary>
    /// <remarks>
    /// Filiz bandında hata sayılmıyor: o bantta amaç örüntü fikrini
    /// yakalamak, doğruyu ilk seferde bulmak değil.
    /// </remarks>
    public PatternOutcome Tap(int choiceId)
    {
        if (Current is not { } question)
        {
            return PatternOutcome.Ignored;
        }

        var choice = question.Choices.FirstOrDefault(c => c.Id == choiceId);
        if (choice is null)
        {
            return PatternOutcome.Ignored;
        }

        if (!choice.IsCorrect)
        {
            if (Band != AgeBand.Filiz)
            {
                Mistakes++;
            }

            return PatternOutcome.Wrong;
        }

        Correct++;
        Current = IsComplete ? null : BuildQuestion();
        return PatternOutcome.Correct;
    }

    private PatternQuestion BuildQuestion()
    {
        var unit = PickUnit();
        _lastUnit = unit;

        var alphabet = BuildAlphabet(unit);

        // Birim baştan sona döngüsel olarak tekrarlanıyor. Sonu yarım kalan
        // bir dizi kusur değil: örüntü zaten sonsuz, ekran onun bir penceresi.
        var full = new PatternTile[_length];
        for (var i = 0; i < _length; i++)
        {
            full[i] = alphabet[unit[i % unit.Length]];
        }

        var blank = PickBlankIndex(unit.Length);

        var sequence = new PatternTile?[_length];
        for (var i = 0; i < _length; i++)
        {
            sequence[i] = i == blank ? null : full[i];
        }

        return new PatternQuestion(sequence, blank, BuildChoices(full[blank], alphabet));
    }

    private string PickUnit()
    {
        if (_units.Length == 1)
        {
            return _units[0];
        }

        // Aynı birim arka arkaya gelmesin: çocuk bakmadan "yine aynısı" deyip
        // doğruyu bulabiliyor. Deneyip tekrar çekmek yerine bir eksik
        // aralıktan çekip son birimin üstünü kaydırıyoruz — kaçınma tesadüfe
        // kalmıyor.
        var last = Array.IndexOf(_units, _lastUnit);
        if (last < 0)
        {
            return _units[_rng.Next(_units.Length)];
        }

        var pick = _rng.Next(_units.Length - 1);
        return _units[pick >= last ? pick + 1 : pick];
    }

    /// <summary>Birimdeki her harfe birbirinden ayırt edilebilir bir parça verir.</summary>
    private Dictionary<char, PatternTile> BuildAlphabet(string unit)
    {
        var letters = unit.Distinct().ToList();

        var hues = Hues.OrderBy(_ => _rng.Next()).Take(letters.Count).ToList();

        // Filiz'de tek şekil: iki boyutta birden değişen bir dizi, örüntüyü
        // henüz kavramamış bir çocuk için iki ayrı bilmece.
        var kinds = _variesShape
            ? Kinds.OrderBy(_ => _rng.Next()).Take(letters.Count).ToList()
            : Enumerable.Repeat(Kinds[_rng.Next(Kinds.Length)], letters.Count).ToList();

        var alphabet = new Dictionary<char, PatternTile>();
        for (var i = 0; i < letters.Count; i++)
        {
            alphabet[letters[i]] = new PatternTile(kinds[i], hues[i]);
        }

        return alphabet;
    }

    /// <summary>
    /// Boşluğun yeri.
    /// </summary>
    /// <remarks>
    /// Boşluktan önce <b>en az bir tam birim</b> duruyor. Aksi hâlde örüntü
    /// diziden okunamıyor ve soru bilmeceye değil kura çekmeye dönüyor.
    /// </remarks>
    private int PickBlankIndex(int unitLength)
    {
        if (!_blankCanBeInside)
        {
            return _length - 1;
        }

        return _rng.Next(unitLength, _length);
    }

    /// <summary>
    /// Seçenekler: doğru parça ve çeldiriciler.
    /// </summary>
    /// <remarks>
    /// Çeldiriciler önce <b>dizinin kendi parçaları</b>: örüntüyü çözemeyen
    /// çocuğun eli oraya gidiyor ve yanlış seçim "rastgele bir şeye bastım"
    /// değil "örüntüyü yanlış okudum" oluyor. Yetmezse aynı şeklin başka
    /// rengi üretiliyor — bu da bir kıl payı, ekrandaki hiçbir şeye
    /// benzemeyen bir parçadan çok daha öğretici.
    /// </remarks>
    private IReadOnlyList<PatternChoice> BuildChoices(
        PatternTile answer, Dictionary<char, PatternTile> alphabet)
    {
        var tiles = new List<PatternTile> { answer };

        foreach (var tile in alphabet.Values)
        {
            if (tiles.Count >= _choiceCount)
            {
                break;
            }

            if (!tiles.Contains(tile))
            {
                tiles.Add(tile);
            }
        }

        foreach (var hue in Hues.OrderBy(_ => _rng.Next()))
        {
            if (tiles.Count >= _choiceCount)
            {
                break;
            }

            var candidate = answer with { Hue = hue };
            if (!tiles.Contains(candidate))
            {
                tiles.Add(candidate);
            }
        }

        return tiles
            .OrderBy(_ => _rng.Next())
            .Select(tile => new PatternChoice(_nextChoiceId++, tile, tile == answer))
            .ToList();
    }
}
