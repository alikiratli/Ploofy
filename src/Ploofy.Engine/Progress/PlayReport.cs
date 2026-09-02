namespace Ploofy.Engine.Progress;

/// <summary>
/// Oynanmış tek bir tur — raporun ham girdisi.
/// </summary>
/// <remarks>
/// Veri katmanındaki satırın motorca görülen hâli. Motor SQLite bilmiyor,
/// bu yüzden dönüştürme uygulama tarafında yapılıyor; buradaki hesabın
/// veritabanı olmadan sınanabilmesinin sebebi de bu.
/// </remarks>
public sealed record PlayedRound(
    DateOnly PlayedOn,
    string GameId,
    AgeBand Band,
    int Stars,
    int Mistakes,
    TimeSpan Duration);

/// <summary>Rapordaki bir gün.</summary>
public sealed record ReportDay(DateOnly Date, int Rounds, TimeSpan Duration, int Stars);

/// <summary>Rapordaki bir oyun.</summary>
public sealed record ReportGame(
    string GameId,
    int Rounds,
    TimeSpan Duration,
    int Stars,
    int BestStars,
    DateOnly LastPlayedOn);

/// <summary>
/// Ebeveyn raporu: son N günün özeti.
/// </summary>
/// <remarks>
/// <para>
/// Ücretli bir çocuk uygulamasında ebeveynin karşılığını gördüğü yer burası:
/// çocuk ne oynadı, ne kadar oynadı, ne kazandı. Hesap motorda duruyor çünkü
/// gözle doğrulanamaz — bir grafiğin çubuğunun doğru yükseklikte olduğu
/// ekrana bakarak anlaşılmıyor.
/// </para>
/// <para>
/// Rapor <b>cihazdan çıkmıyor</b>. Kaynağı yerel veritabanı, okuyucusu
/// ebeveyn kilidinin arkasındaki ekran; gizlilik politikasındaki "hiçbir veri
/// gönderilmez" cümlesi burada da geçerli.
/// </para>
/// </remarks>
public sealed class PlayReport
{
    /// <summary>
    /// Tek bir turun sayılabileceği en uzun süre.
    /// </summary>
    /// <remarks>
    /// Süre, tur başlarken çalışan bir kronometreden geliyor ve uygulama arka
    /// plana atıldığında kronometre durmuyor. Cihazı bırakıp akşam dönen bir
    /// çocuk, kırpma olmadan "bugün 6 saat oynadı" satırı üretiyor — ve bu
    /// tek satır bütün raporu yalancı yapıyor.
    ///
    /// On beş dakika kasıtlı olarak cömert: en uzun tur (Meşe bandında
    /// Yapboz'un on altı parçası) bunun çok altında kalıyor, yani kırpma
    /// yalnızca gerçekten bırakılmış turları yakalıyor.
    /// </remarks>
    public static readonly TimeSpan LongestCountedRound = TimeSpan.FromMinutes(15);

    private PlayReport(
        DateOnly from,
        DateOnly to,
        IReadOnlyList<ReportDay> days,
        IReadOnlyList<ReportGame> games)
    {
        From = from;
        To = to;
        Days = days;
        Games = games;
    }

    /// <summary>Raporun ilk günü.</summary>
    public DateOnly From { get; }

    /// <summary>Raporun son günü — genellikle bugün.</summary>
    public DateOnly To { get; }

    /// <summary>
    /// Her gün için bir satır, eskiden yeniye.
    /// </summary>
    /// <remarks>
    /// Oynanmamış günler de <b>listede</b>, sıfır değerle: grafikte boş günün
    /// yerini boş bırakmak, hafta sonu oynanmadığını gösteren tek şey. Atlanan
    /// bir gün, çubukları yan yana getirip eğilimi olduğundan düzgün
    /// gösteriyor.
    /// </remarks>
    public IReadOnlyList<ReportDay> Days { get; }

    /// <summary>Oynanan oyunlar, çok oynanandan aza.</summary>
    public IReadOnlyList<ReportGame> Games { get; }

    public int TotalRounds => Days.Sum(d => d.Rounds);

    public int TotalStars => Days.Sum(d => d.Stars);

    public TimeSpan TotalDuration =>
        TimeSpan.FromTicks(Days.Sum(d => d.Duration.Ticks));

    /// <summary>Hiç oynanmadıysa ekran "henüz veri yok" diyor.</summary>
    public bool IsEmpty => TotalRounds == 0;

    /// <summary>Oynanmış gün sayısı — "on dört günün dokuzunda oynadı".</summary>
    public int ActiveDays => Days.Count(d => d.Rounds > 0);

    /// <summary>
    /// En yoğun günün süresi. Grafiğin ölçeği bundan çıkıyor.
    /// </summary>
    /// <remarks>
    /// Sıfır olabilir (hiç oynanmamışsa); grafiği çizen taraf buna karşı
    /// kendini korumalı.
    /// </remarks>
    public TimeSpan BusiestDay =>
        Days.Count == 0 ? TimeSpan.Zero : Days.Max(d => d.Duration);

    /// <summary>
    /// Raporu kurar.
    /// </summary>
    /// <param name="rounds">
    /// Oynanmış turlar. Aralık dışındakiler sessizce eleniyor — çağıranın
    /// tam olarak doğru aralığı okumuş olması gerekmiyor.
    /// </param>
    /// <param name="today">Raporun son günü.</param>
    /// <param name="days">Kaç günü kapsayacağı, bugün dahil.</param>
    public static PlayReport Build(
        IReadOnlyList<PlayedRound> rounds, DateOnly today, int days)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(days, 1);

        var from = today.AddDays(-(days - 1));

        var inRange = rounds
            .Where(r => r.PlayedOn >= from && r.PlayedOn <= today)
            .ToList();

        var byDay = inRange.ToLookup(r => r.PlayedOn);

        var dayRows = new List<ReportDay>(days);
        for (var i = 0; i < days; i++)
        {
            var date = from.AddDays(i);
            var played = byDay[date].ToList();

            dayRows.Add(new ReportDay(
                date,
                played.Count,
                Total(played),
                played.Sum(r => r.Stars)));
        }

        var gameRows = inRange
            .GroupBy(r => r.GameId, StringComparer.Ordinal)
            .Select(g => new ReportGame(
                g.Key,
                g.Count(),
                Total(g),
                g.Sum(r => r.Stars),
                g.Max(r => r.Stars),
                g.Max(r => r.PlayedOn)))
            .OrderByDescending(g => g.Rounds)
            .ThenByDescending(g => g.LastPlayedOn)
            .ThenBy(g => g.GameId, StringComparer.Ordinal)
            .ToList();

        return new PlayReport(from, today, dayRows, gameRows);
    }

    /// <summary>Sürelerin toplamı; her tur ayrı ayrı kırpılarak.</summary>
    private static TimeSpan Total(IEnumerable<PlayedRound> rounds) =>
        TimeSpan.FromTicks(rounds.Sum(r => Clamp(r.Duration).Ticks));

    /// <summary>
    /// Bir turun sayılan süresi.
    /// </summary>
    /// <remarks>
    /// Negatif süre de kırpılıyor: cihazın saati geri alındığında kronometre
    /// eksi değer üretebiliyor ve eksi bir gün, grafikte olmayan bir düşüş
    /// gösteriyor.
    /// </remarks>
    private static TimeSpan Clamp(TimeSpan duration) =>
        duration < TimeSpan.Zero
            ? TimeSpan.Zero
            : duration > LongestCountedRound ? LongestCountedRound : duration;
}
