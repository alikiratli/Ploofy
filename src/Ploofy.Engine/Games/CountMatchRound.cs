using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Bir rakama bırakmanın sonucu.</summary>
public enum CountOutcome
{
    /// <summary>Bırakma yok sayıldı (ekranda olmayan rakam, tur bitmiş).</summary>
    Ignored,

    Correct,

    /// <summary>Yanlış rakam — grup yerine dönüyor, soru değişmiyor.</summary>
    Wrong,
}

/// <summary>
/// Sayılacak nesne kümesi.
/// </summary>
/// <param name="Kind">
/// Kümedeki bütün nesneler aynı şekil. Karışık şekil vermek "kaç tane"
/// sorusunu sessizce "kaç tane daire" sorusuna çeviriyor ve çocuk hangisini
/// sayacağını bilemiyor.
/// </param>
public sealed record CountGroup(int Count, ShapeKind Kind, BubbleHue Hue);

/// <summary>Tek bir soru: sayılacak küme ve rakam seçenekleri.</summary>
/// <param name="Choices">
/// Küçükten büyüğe sıralı. Sıralı olması bilinçli: tepsi küçük bir sayı
/// doğrusu gibi okunuyor, henüz güvenle sayamayan çocuk bile "daha çok olan
/// daha sağda" ilişkisini görüyor.
/// </param>
public sealed record CountQuestion(CountGroup Group, IReadOnlyList<int> Choices);

/// <summary>Say ve Eşleştir'in banda göre zorluk tablosu.</summary>
public static class CountMatchTuning
{
    /// <summary>Turdaki soru sayısı.</summary>
    public static readonly BandValue<int> Questions = new(4, 6, 8);

    /// <summary>Ekrandaki rakam seçeneği sayısı.</summary>
    public static readonly BandValue<int> Choices = new(2, 3, 4);

    /// <summary>Sayılabilecek en büyük miktar.</summary>
    /// <remarks>
    /// Filiz 3'te duruyor: bu yaşta üçe kadar olan miktar <b>sayılmadan</b>
    /// görülüyor (subitizing) ve oyun tam olarak bu eşiği pekiştiriyor.
    /// Fidan 5, Meşe 10 — parmak sayısı sınırı, sonraki adım.
    /// </remarks>
    public static readonly BandValue<int> MaxCount = new(3, 5, 10);

    /// <summary>
    /// Çeldirici rakamlar hedefin komşusu mu olsun?
    /// </summary>
    /// <remarks>
    /// Bandın asıl farkı bu. 7 nesneyi 2 ile 7 arasından ayırmak <b>tahmin</b>;
    /// 6 ile 7 arasından ayırmak <b>gerçekten saymak</b>. İkincisi ancak
    /// Meşe'de anlamlı, çünkü altındaki bantlarda tek tek sayma henüz
    /// oturmamış oluyor ve her soru hataya dönüyor.
    /// </remarks>
    public static readonly BandValue<bool> UseNeighbours = new(false, false, true);

    /// <summary>
    /// Nesneler düzenli sıra yerine dağınık mı dizilsin?
    /// </summary>
    /// <remarks>
    /// Sıra hâlinde dizilmiş nesneleri saymak dağınık duranları saymaktan
    /// belirgin şekilde kolay: sırada parmak soldan sağa gidiyor, dağınıkta
    /// çocuğun saydığını ve saymadığını kendi kafasında ayırması gerekiyor.
    /// Yerleşim arayüzde yapılıyor ama <i>kararı</i> burada, çünkü bu bir
    /// süs değil zorluk knob'u.
    /// </remarks>
    public static readonly BandValue<bool> ScattersItems = new(false, false, true);

    /// <summary>Üçüncü yıldız için hedef süre (yalnızca Meşe).</summary>
    public static readonly BandValue<TimeSpan?> ParTime = new(
        null,
        null,
        TimeSpan.FromSeconds(45));
}

/// <summary>
/// Say ve Eşleştir'in kuralları — arayüzden bağımsız.
/// </summary>
/// <remarks>
/// <para>
/// Ekranda bir nesne kümesi ve birkaç rakam duruyor; çocuk kümeyi doğru
/// rakamın üstüne sürüklüyor. Miktarı rakama bağlamak, Sayı Avı'nın
/// öğrettiği "rakamı tanıma"nın bir sonraki adımı: rakam artık bir şekil
/// değil, bir <b>sayı</b>.
/// </para>
/// <para>
/// Yönerge yazıya bağlı değil — sayılacak küme ve rakamlar aynı ekranda
/// duruyor, okuma gerekmiyor.
/// </para>
/// </remarks>
public sealed class CountMatchRound
{
    private static readonly ShapeKind[] Kinds = Enum.GetValues<ShapeKind>();
    private static readonly BubbleHue[] Hues = Enum.GetValues<BubbleHue>();

    private readonly Random _rng;
    private readonly int _maxCount;
    private readonly int _choiceCount;
    private readonly bool _useNeighbours;

    /// <summary>Aynı miktarın arka arkaya gelmemesi için son sorulan.</summary>
    private int _lastCount;

    private CountMatchRound(AgeBand band, Random rng)
    {
        Band = band;
        _rng = rng;

        Total = CountMatchTuning.Questions.For(band);
        _maxCount = CountMatchTuning.MaxCount.For(band);
        _choiceCount = Math.Min(CountMatchTuning.Choices.For(band), _maxCount);
        _useNeighbours = CountMatchTuning.UseNeighbours.For(band);

        ScattersItems = CountMatchTuning.ScattersItems.For(band);
        ParTime = CountMatchTuning.ParTime.For(band);

        Current = BuildQuestion();
    }

    /// <summary>Bant için standart bir tur kurar. <paramref name="random"/> testlerde sabitlenebilir.</summary>
    public static CountMatchRound ForBand(AgeBand band, Random? random = null) =>
        new(band, random ?? Random.Shared);

    public AgeBand Band { get; }

    /// <summary>Nesneler dağınık mı dizilecek? Arayüz bunu okuyor.</summary>
    public bool ScattersItems { get; }

    public TimeSpan? ParTime { get; }

    /// <summary>Turdaki toplam soru sayısı.</summary>
    public int Total { get; }

    public int Correct { get; private set; }

    public int Mistakes { get; private set; }

    /// <summary>Sıradaki soru; tur bittiyse null.</summary>
    public CountQuestion? Current { get; private set; }

    public bool IsComplete => Correct >= Total;

    /// <summary>
    /// Kümeyi bir rakamın üstüne bırakır.
    /// </summary>
    /// <remarks>
    /// Yanlış rakamda soru <b>değişmiyor</b>: küme yerine dönüyor ve çocuk
    /// yeniden sayabiliyor. Doğruyu görmeden geçmek, oyunun öğretici olma
    /// iddiasını boşa çıkarırdı — Harf/Sayı Avı'ndaki karar burada da geçerli.
    /// </remarks>
    public CountOutcome Drop(int digit)
    {
        if (Current is not { } question)
        {
            return CountOutcome.Ignored;
        }

        if (!question.Choices.Contains(digit))
        {
            return CountOutcome.Ignored;
        }

        if (digit != question.Group.Count)
        {
            // Filiz bandında hata sayılmıyor; zaten bu oyun Fidan'dan
            // itibaren katalogda görünüyor ama motor bandı varsayamaz.
            if (Band != AgeBand.Filiz)
            {
                Mistakes++;
            }

            return CountOutcome.Wrong;
        }

        Correct++;
        Current = IsComplete ? null : BuildQuestion();
        return CountOutcome.Correct;
    }

    private CountQuestion BuildQuestion()
    {
        var count = PickCount();
        _lastCount = count;

        var group = new CountGroup(
            count,
            Kinds[_rng.Next(Kinds.Length)],
            Hues[_rng.Next(Hues.Length)]);

        var choices = new List<int>(_choiceCount) { count };
        choices.AddRange(PickDistractors(count));
        choices.Sort();

        return new CountQuestion(group, choices);
    }

    private int PickCount()
    {
        if (_lastCount == 0)
        {
            return _rng.Next(1, _maxCount + 1);
        }

        // Aynı miktar arka arkaya gelmesin: çocuk saymadan "yine aynısı" deyip
        // doğruyu bulabiliyor ve oyun bir şey öğretmiyor. Deneyip tekrar
        // çekmek yerine bir eksik aralıktan çekip son miktarın üstünü
        // kaydırıyoruz — böylece kaçınma tesadüfe kalmıyor.
        var pick = _rng.Next(1, _maxCount);
        return pick >= _lastCount ? pick + 1 : pick;
    }

    private IEnumerable<int> PickDistractors(int target)
    {
        var needed = _choiceCount - 1;

        var all = Enumerable
            .Range(1, _maxCount)
            .Where(n => n != target)
            .ToList();

        if (_useNeighbours)
        {
            // Elde olan en yakın sayılar. Aralığın ucunda pencere
            // kendiliğinden genişliyor (10'un sağında komşu yok) — orada da
            // 2 koymaktansa 7 koymak soruyu ayakta tutuyor.
            return all
                .OrderBy(n => Math.Abs(n - target))
                .ThenBy(_ => _rng.Next())
                .Take(needed)
                .ToList();
        }

        // Uzak çeldiriciler: sayamayan çocuk miktar hissiyle de geçebilsin.
        var far = all.Where(n => Math.Abs(n - target) > 2).ToList();
        var near = all.Where(n => Math.Abs(n - target) <= 2).ToList();

        var chosen = far.OrderBy(_ => _rng.Next()).Take(needed).ToList();

        // Uzak sayı yetmezse (Filiz'in 1-3 aralığında hiç yok) yakınla
        // tamamlanıyor: seçenek sayısı her soruda aynı kalmalı.
        if (chosen.Count < needed)
        {
            chosen.AddRange(near.OrderBy(_ => _rng.Next()).Take(needed - chosen.Count));
        }

        return chosen;
    }
}
