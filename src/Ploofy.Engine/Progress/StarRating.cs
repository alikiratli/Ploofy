namespace Ploofy.Engine.Progress;

/// <summary>
/// Turdan yıldıza çeviren tek yer.
/// </summary>
/// <remarks>
/// Kural bantla birlikte değişir, çünkü aynı ölçüt üç yaş grubunda üç farklı
/// anlam taşıyor: 3 yaşındaki için "bitirdim" başarıdır, 8 yaşındaki için
/// "hatasız ve hızlı bitirdim" başarıdır. Yıldızı oyunlara bırakmak, her yeni
/// oyunda bu dengenin yeniden ve tutarsız kurulması demek olurdu.
/// </remarks>
public static class StarRating
{
    public const int MaxStars = 3;

    public static int ForOutcome(RoundOutcome outcome)
    {
        if (!outcome.Completed)
        {
            // Yarım kalan tur: Filiz'de yine de bir yıldız verilir (dokunmuş,
            // denemiş; ödülsüz bırakmanın bu yaşta karşılığı yok).
            return outcome.Band == AgeBand.Filiz ? 1 : 0;
        }

        return outcome.Band switch
        {
            // Filiz: bitirmek yeterli. Hata sayılmaz, süre sayılmaz.
            AgeBand.Filiz => MaxStars,

            // Fidan: hedef var ama ceza yumuşak — birkaç hata üç yıldızı bozmaz.
            AgeBand.Fidan => outcome.Mistakes switch
            {
                <= 1 => 3,
                <= 3 => 2,
                _ => 1,
            },

            // Meşe: hatasızlık üçüncü yıldızın ön koşulu, süre de sayılır.
            AgeBand.Mese => MeseStars(outcome),

            _ => throw new ArgumentOutOfRangeException(nameof(outcome)),
        };
    }

    private static int MeseStars(RoundOutcome outcome)
    {
        if (outcome.Mistakes == 0)
        {
            return outcome.ParTime is null || outcome.Elapsed <= outcome.ParTime ? 3 : 2;
        }

        return outcome.Accuracy >= 0.75d ? 2 : 1;
    }

    /// <summary>
    /// Turun ham puanı — sıralı oyunda oyuncuları karşılaştırmak için.
    /// </summary>
    /// <remarks>
    /// Yıldız çocuğa gösterilen ödül, puan ise iki çocuğu kıyaslayan sayı.
    /// Ayrı tutuluyorlar: küçük kardeş Filiz bandında oynadığı için her turda
    /// üç yıldız alır, ama sıralamada bu ona otomatik üstünlük vermemeli.
    /// </remarks>
    public static int RawScore(RoundOutcome outcome) =>
        Math.Max(0, (outcome.Correct * 10) - (outcome.Mistakes * 3));
}
