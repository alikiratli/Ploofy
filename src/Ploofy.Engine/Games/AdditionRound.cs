using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>Bir cevabın sonucu.</summary>
public enum AnswerOutcome
{
    /// <summary>Tur bitmiş ya da cevap seçeneklerde yok — sayılmıyor.</summary>
    Ignored,

    Correct,

    Wrong,
}

/// <summary>
/// Tek bir toplama sorusu.
/// </summary>
/// <param name="Left">Birinci toplanan.</param>
/// <param name="Right">İkinci toplanan.</param>
/// <param name="Choices">Seçenekler, ekrandaki sırayla. Biri doğru cevap.</param>
/// <param name="Glyph">Nesnelerin simgesi — bir soruda tek tür nesne var.</param>
public sealed record AdditionQuestion(
    int Left,
    int Right,
    IReadOnlyList<int> Choices,
    string Glyph)
{
    public int Answer => Left + Right;
}

/// <summary>Basit Toplama'nın banda göre zorluk tablosu.</summary>
public static class AdditionTuning
{
    /// <summary>Bir turdaki soru sayısı.</summary>
    public static readonly BandValue<int> Questions = new(4, 5, 6);

    /// <summary>Toplamın en büyük değeri.</summary>
    /// <remarks>
    /// Fidan'da beş: 4-6 yaş bir elin parmaklarıyla sayıyor ve beşi geçen
    /// toplam, toplamayı değil saymayı zorlaştırıyor. Meşe'de on — iki elin
    /// sınırı ve okul öncesinin doğal üst sınırı.
    /// </remarks>
    public static readonly BandValue<int> MaxSum = new(5, 5, 10);

    /// <summary>Kaç seçenek gösteriliyor.</summary>
    public static readonly BandValue<int> ChoiceCount = new(3, 3, 4);

    /// <summary>
    /// Birinci toplanan da nesne olarak gösteriliyor mu?
    /// </summary>
    /// <remarks>
    /// Bandın asıl farkı bu. Fidan'da iki küme de nesne: çocuk hepsini
    /// baştan sayıyor ("üç elma ve iki elma, bir iki üç dört beş"). Meşe'de
    /// birinci toplanan <b>rakam</b>, ikincisi nesne — yani "üçten devam et,
    /// dört beş". Saymanın bir sonraki adımı tam olarak bu ve iki aşama
    /// arasındaki fark, aynı oyunu iki farklı beceriye çeviriyor.
    ///
    /// Nesneler Meşe'de de tamamen kaldırılmadı: kaldırmak oyunu ezberlenmiş
    /// toplama tablosuna çevirirdi, oysa amaç saymanın kendisi.
    /// </remarks>
    public static readonly BandValue<bool> ShowsFirstAsObjects = new(true, true, false);

    /// <summary>Yanlış cevap yıldızı düşürüyor mu?</summary>
    public static readonly BandValue<bool> CountsMistakes = new(true, true, true);

    /// <summary>Üçüncü yıldız için hedef süre (yalnızca Meşe).</summary>
    public static readonly BandValue<TimeSpan?> ParTime = new(
        null,
        null,
        TimeSpan.FromSeconds(75));

    /// <summary>
    /// Sayılabilecek nesneler.
    /// </summary>
    /// <remarks>
    /// Hepsi tek parça ve aynı boyda görünüyor: bir kümedeki dokuz tanesi
    /// yan yana dizildiğinde ayırt edilebilmeli. Unicode 6 ve öncesi —
    /// uygulamanın alt sınırı Android 8.0.
    /// </remarks>
    public static readonly IReadOnlyList<string> Objects =
        ["🍎", "🍌", "🍓", "⭐", "🐟", "🌸", "🍪", "🎈"];
}

/// <summary>
/// Basit Toplama turu.
/// </summary>
/// <remarks>
/// <para>
/// Say ve Eşleştir'in devamı: orada miktar bir rakamla eşleniyordu, burada
/// iki miktar birleşiyor. Noktaları Birleştir'in kurduğu sayı doğrusu fikri
/// (birden sonra iki gelir) bu oyunun tam altında duruyor — toplama, o
/// doğruda ileri gitmenin adı.
/// </para>
/// <para>
/// Kaybetme yok: yanlış seçenek soruyu geçirmiyor, çocuk doğruyu bulana
/// kadar deneyebiliyor. Yalnızca yıldız etkileniyor.
/// </para>
/// <para>
/// Filiz bandı bu oyunu hiç görmüyor — 2-4 yaş toplamıyor, o yaşın
/// karşılığı Say ve Eşleştir'in kendisi.
/// </para>
/// </remarks>
public sealed class AdditionRound
{
    private readonly List<AdditionQuestion> _queue;

    private int _index;

    private AdditionRound(AgeBand band, List<AdditionQuestion> questions)
    {
        Band = band;
        _queue = questions;
        Total = questions.Count;
        ShowsFirstAsObjects = AdditionTuning.ShowsFirstAsObjects.For(band);
        CountsMistakes = AdditionTuning.CountsMistakes.For(band);
        ParTime = AdditionTuning.ParTime.For(band);
    }

    public static AdditionRound ForBand(AgeBand band, Random? random = null)
    {
        var rng = random ?? Random.Shared;

        var count = AdditionTuning.Questions.For(band);
        var maxSum = AdditionTuning.MaxSum.For(band);
        var choiceCount = AdditionTuning.ChoiceCount.For(band);

        var questions = new List<AdditionQuestion>(count);
        var lastAnswer = -1;

        for (var i = 0; i < count; i++)
        {
            AdditionQuestion question;
            var guard = 0;

            do
            {
                question = BuildQuestion(rng, maxSum, choiceCount);
                guard++;
            }
            // Arka arkaya aynı cevap gelmesin: çocuk soruya bakmadan
            // önceki kutucuğa dokunmayı öğreniyor. Guard, dar aralıkta
            // (Fidan'da toplam en fazla 5) sonsuz döngüyü engelliyor.
            while (question.Answer == lastAnswer && guard < 20);

            lastAnswer = question.Answer;
            questions.Add(question);
        }

        return new AdditionRound(band, questions);
    }

    public AgeBand Band { get; }

    public bool ShowsFirstAsObjects { get; }

    public bool CountsMistakes { get; }

    public TimeSpan? ParTime { get; }

    public int Total { get; }

    /// <summary>Doğru cevaplanmış soru sayısı.</summary>
    public int Answered { get; private set; }

    /// <summary>Yanlış seçenek sayısı, sayılıp sayılmadığından bağımsız.</summary>
    public int WrongAnswers { get; private set; }

    public int Mistakes => CountsMistakes ? WrongAnswers : 0;

    public bool IsComplete => Answered >= Total;

    /// <summary>Şu anki soru; tur bittiyse null.</summary>
    public AdditionQuestion? Current =>
        _index < _queue.Count ? _queue[_index] : null;

    /// <summary>
    /// Bir seçeneğe dokunulur.
    /// </summary>
    /// <remarks>
    /// Yanlışta soru <b>değişmiyor</b>: çocuk aynı soruyu doğru cevaplayana
    /// kadar deneyebiliyor. Kütüphanedeki bütün oyunlarda aynı kural —
    /// yanlış cevap "tekrar dene" demek, "kaybettin" değil.
    /// </remarks>
    public AnswerOutcome Answer(int value)
    {
        if (Current is not { } question)
        {
            return AnswerOutcome.Ignored;
        }

        if (!question.Choices.Contains(value))
        {
            return AnswerOutcome.Ignored;
        }

        if (value != question.Answer)
        {
            WrongAnswers++;
            return AnswerOutcome.Wrong;
        }

        Answered++;
        _index++;
        return AnswerOutcome.Correct;
    }

    private static AdditionQuestion BuildQuestion(Random rng, int maxSum, int choiceCount)
    {
        // İki toplanan da en az bir: sıfırlı toplama ("üç artı sıfır") bu
        // yaşta öğretici değil, kafa karıştırıcı.
        var left = rng.Next(1, maxSum);
        var right = rng.Next(1, maxSum - left + 1);

        var answer = left + right;
        var glyph = AdditionTuning.Objects[rng.Next(AdditionTuning.Objects.Count)];

        return new AdditionQuestion(
            left,
            right,
            BuildChoices(rng, answer, choiceCount, maxSum),
            glyph);
    }

    /// <summary>
    /// Seçenekleri kurar: doğru cevap ve ona yakın çeldiriciler.
    /// </summary>
    /// <remarks>
    /// Çeldiriciler <b>komşu sayılar</b>, rastgele değil. Uzak bir çeldirici
    /// (7 için 2) elenmek için toplamayı gerektirmiyor, yalnızca "büyükçe
    /// bir sayı" demek yetiyor. Bir eksik ve bir fazla ise ancak gerçekten
    /// sayarak elenebiliyor.
    /// </remarks>
    private static IReadOnlyList<int> BuildChoices(
        Random rng, int answer, int choiceCount, int maxSum)
    {
        var choices = new List<int> { answer };

        // Önce en yakın komşular, sonra gerekiyorsa uzaklaşarak.
        for (var distance = 1; choices.Count < choiceCount && distance <= maxSum; distance++)
        {
            foreach (var candidate in new[] { answer - distance, answer + distance }
                         .OrderBy(_ => rng.Next()))
            {
                if (choices.Count >= choiceCount)
                {
                    break;
                }

                // Sıfır ve altı seçenek olmuyor: iki pozitif sayının toplamı
                // hiçbir zaman oraya düşmüyor ve çocuk onu bakmadan eliyor.
                if (candidate >= 1 && candidate <= maxSum + 2 && !choices.Contains(candidate))
                {
                    choices.Add(candidate);
                }
            }
        }

        return [.. choices.OrderBy(_ => rng.Next())];
    }
}
