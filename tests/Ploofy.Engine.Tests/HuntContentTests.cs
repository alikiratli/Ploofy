using Ploofy.Engine;
using Ploofy.Engine.Games;

namespace Ploofy.Engine.Tests;

/// <summary>
/// Av oyununun dile ve banda bağlı davranışı.
/// </summary>
/// <remarks>
/// Havuzun kendisi uygulama katmanında (alfabe dile göre değişiyor), ama
/// motorun o havuzla ne yaptığı burada sınanıyor.
/// </remarks>
public class HuntContentTests
{
    private static readonly string[] TurkishUpper =
    [
        "A", "B", "C", "Ç", "D", "E", "F", "G", "Ğ", "H", "I", "İ", "J", "K", "L",
        "M", "N", "O", "Ö", "P", "R", "S", "Ş", "T", "U", "Ü", "V", "Y", "Z",
    ];

    /// <summary>Verilen hedef için üretilen çeldiricileri toplar.</summary>
    private static HashSet<string> DistractorsFor(
        string target,
        IReadOnlyList<string> pool,
        int attempts = 300)
    {
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var seed = 0; seed < attempts; seed++)
        {
            var round = HuntRound.ForBand(AgeBand.Mese, HuntKind.Letter, pool, new Random(seed));

            while (round.Current is { } question)
            {
                if (string.Equals(question.Target, target, StringComparison.Ordinal))
                {
                    foreach (var choice in question.Choices.Where(c => !c.IsTarget))
                    {
                        seen.Add(choice.Glyph);
                    }
                }

                round.Tap(question.Choices.Single(c => c.IsTarget).Id);
            }
        }

        return seen;
    }

    [Fact]
    public void Turkish_dotted_and_dotless_I_are_offered_together()
    {
        // Okumaya geçişte en çok takılınan Türkçe ayrımı; oyunun çözmesi
        // gereken şey tam olarak bu.
        Assert.Contains("İ", DistractorsFor("I", TurkishUpper));
        Assert.Contains("I", DistractorsFor("İ", TurkishUpper));
    }

    [Theory]
    [InlineData("O", "Ö")]
    [InlineData("U", "Ü")]
    [InlineData("C", "Ç")]
    [InlineData("S", "Ş")]
    [InlineData("G", "Ğ")]
    public void Turkish_accent_pairs_are_offered_together(string plain, string accented)
    {
        Assert.Contains(accented, DistractorsFor(plain, TurkishUpper));
    }

    [Fact]
    public void An_english_pool_never_shows_a_turkish_letter()
    {
        // Aynı çeldirici tablosu üç dilde de kullanılıyor; havuzda olmayan
        // işaret elenmeli.
        string[] english =
        [
            "A", "B", "C", "D", "E", "F", "G", "H", "I", "J", "K", "L", "M",
            "N", "O", "P", "Q", "R", "S", "T", "U", "V", "W", "X", "Y", "Z",
        ];

        var turkishOnly = new[] { "Ç", "Ğ", "İ", "Ö", "Ş", "Ü" };

        for (var seed = 0; seed < 60; seed++)
        {
            var round = HuntRound.ForBand(AgeBand.Mese, HuntKind.Letter, english, new Random(seed));

            while (round.Current is { } question)
            {
                Assert.All(
                    question.Choices,
                    c => Assert.DoesNotContain(c.Glyph, turkishOnly));

                round.Tap(question.Choices.Single(c => c.IsTarget).Id);
            }
        }
    }

    [Fact]
    public void Two_digit_numbers_get_digit_order_distractors()
    {
        // 12 ile 21'i karıştırmak bu yaşta gerçek bir hata kaynağı.
        var pool = Enumerable.Range(0, 26).Select(n => n.ToString()).ToList();
        var seen = new HashSet<string>(StringComparer.Ordinal);

        for (var seed = 0; seed < 300; seed++)
        {
            var round = HuntRound.ForBand(AgeBand.Mese, HuntKind.Number, pool, new Random(seed));

            while (round.Current is { } question)
            {
                if (question.Target == "12")
                {
                    foreach (var choice in question.Choices.Where(c => !c.IsTarget))
                    {
                        seen.Add(choice.Glyph);
                    }
                }

                round.Tap(question.Choices.Single(c => c.IsTarget).Id);
            }
        }

        Assert.Contains("21", seen);
    }
}
