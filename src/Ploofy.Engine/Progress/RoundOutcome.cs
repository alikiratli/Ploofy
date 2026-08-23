namespace Ploofy.Engine.Progress;

/// <summary>Bir turun ham sonucu. Her mini oyun turu bitirirken bunu üretir.</summary>
/// <param name="Completed">
/// Tur sonuna kadar gidildi mi? Filiz ve Fidan bantlarında bu neredeyse her
/// zaman true — o bantlarda kaybetme yok, çocuk ya bitirir ya çıkar.
/// </param>
/// <param name="ParTime">
/// "İyi bir süre" referansı. Yalnızca Meşe bandında üçüncü yıldız için
/// kullanılır; oyun kendi zorluk tablosundan verir.
/// </param>
public sealed record RoundOutcome(
    string GameId,
    int ProfileId,
    AgeBand Band,
    bool Completed,
    int Correct,
    int Mistakes,
    TimeSpan Elapsed,
    TimeSpan? ParTime = null)
{
    public int Attempts => Correct + Mistakes;

    public double Accuracy => Attempts == 0 ? 1d : (double)Correct / Attempts;
}
