using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Progress;

/// <summary>
/// Bir çocuğun bugünkü oyun süresi bütçesinin durumu.
/// </summary>
/// <param name="Limit">Günlük sınır; sınırsızsa <see cref="TimeSpan.Zero"/>.</param>
/// <param name="Used">Bugün oynanmış süre.</param>
public readonly record struct ScreenTimeStatus(TimeSpan Limit, TimeSpan Used)
{
    /// <summary>Ebeveyn sınır koymamış.</summary>
    public bool IsUnlimited => Limit <= TimeSpan.Zero;

    /// <summary>Kalan süre; sınırsızsa <see cref="TimeSpan.Zero"/>.</summary>
    public TimeSpan Remaining =>
        IsUnlimited ? TimeSpan.Zero : Limit - Used > TimeSpan.Zero ? Limit - Used : TimeSpan.Zero;

    /// <summary>Bütçe bitti — yeni tur başlamıyor.</summary>
    public bool IsSpent => !IsUnlimited && Used >= Limit;

    /// <summary>
    /// Bütçe bitmedi ama bir tur daha alacak kadar kaldı.
    /// </summary>
    /// <remarks>
    /// Tur sonu ekranı burada "son bir oyun kaldı" diyor. Uyarı ekranda geri
    /// sayan bir saatle değil, tek bir cümleyle veriliyor: bu yaşta görünür
    /// bir sayaç kaygı üretiyor ve çocuğu oyunu bitirmeye değil acele etmeye
    /// itiyor.
    /// </remarks>
    public bool IsLastRound =>
        !IsUnlimited && !IsSpent && Remaining <= ScreenTimeBudget.LastRoundWarning;

    /// <summary>Harcanan oran, 0-1. Ebeveyn ekranındaki çubuk bundan çıkıyor.</summary>
    public double Fraction =>
        IsUnlimited ? 0d : Math.Clamp(Used.Ticks / (double)Limit.Ticks, 0d, 1d);
}

/// <summary>
/// Günlük oyun süresi bütçesi.
/// </summary>
/// <remarks>
/// <para>
/// Ebeveynin "yeter artık" pazarlığını uygulamaya devretmesi için. Sınır
/// dolduğunda oyun listesi kapanıyor ve ertesi gün kendiliğinden açılıyor;
/// ebeveynin kötü adam olması gerekmiyor.
/// </para>
/// <para>
/// <b>Sınır profil başına ve varsayılan olarak kapalı.</b> Kapalı olması
/// şart: açık gelseydi güncellemeden sonra bütün çocuklar birden kilitlenir
/// ve kimse sebebini bilmezdi. Ebeveyn açtığında bandına göre bir öneri
/// geliyor ama son söz onun — bir ailenin tabletle ilişkisini uygulama
/// bilemez, yaşa göre dayatılan bir sınır küstahlık olurdu.
/// </para>
/// <para>
/// <b>Ölçülen şey oyun süresi, ekran süresi değil.</b> Kaynak
/// <c>round_history</c>: yalnızca oynanmış turlar sayılıyor, ana ekranda ya
/// da koleksiyonda geçen süre değil. İkisi de savunulabilir ama bu, raporun
/// zaten ölçtüğü şey — iki ayrı sayı tutmak, ebeveyne birbirini tutmayan iki
/// rakam göstermek olurdu. Arayüzde de "oyun süresi" deniyor, "ekran süresi"
/// değil.
/// </para>
/// <para>
/// Turlar <see cref="PlayReport.LongestCountedRound"/> ile kırpılıyor. Aynı
/// sebep: kronometre uygulama arka plandayken durmuyor ve bırakılmış tek bir
/// tur, kırpma olmadan çocuğun bütün gününü yakardı.
/// </para>
/// <para>
/// Gün <b>yerel</b> saatle dönüyor — <c>RoundHistoryRow.PlayedAtLocal</c>
/// zaten yerel saatle yazılıyor, tam bu yüzden. Gece 22:00'de oynanan bir
/// tur ebeveyn için bugün, UTC'de yarın olurdu.
/// </para>
/// </remarks>
public static class ScreenTimeBudget
{
    /// <summary>Sınırsızı temsil eden dakika değeri.</summary>
    /// <remarks>
    /// Ayrı bir bayrak yerine sıfır: ayar tablosunda tek bir tam sayı duruyor
    /// ve "sınır yok" ile "sınır sıfır dakika" arasında anlamlı bir fark yok —
    /// ikisi de "oynayamaz" demek olurdu, ki istenmeyen tek şey o.
    /// </remarks>
    public const int Unlimited = 0;

    /// <summary>Bütçenin sonuna bu kadar kalınca "son oyun" uyarısı çıkıyor.</summary>
    /// <remarks>
    /// <para>
    /// Beş dakika, tipik bir turdan (bir-üç dakika) rahatça uzun: uyarı
    /// çıktığında çocuğun gerçekten bir tur daha oynayacak süresi var.
    /// "Son oyun" dedikten sonra oyuna sokmamak uyarıyı yalan yapardı.
    /// </para>
    /// <para>
    /// Üst sınırı da var: en küçük seçenekten (10 dakika) kısa olmak zorunda,
    /// yoksa o sınırı seçen ebeveynin çocuğu daha ilk turdan "son oyun"
    /// uyarısıyla karşılaşırdı. <c>ScreenTimeBudgetTests</c> bunu sınıyor.
    /// </para>
    /// <para>
    /// <see cref="PlayReport.LongestCountedRound"/> ile karıştırılmamalı: o,
    /// bırakılmış turları yakalayan bir kırpma sınırı, turların gerçek
    /// uzunluğu değil.
    /// </para>
    /// </remarks>
    public static readonly TimeSpan LastRoundWarning = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Ebeveyn sınırı açtığında seçili gelen dakika, banda göre.
    /// </summary>
    /// <remarks>
    /// Öneri, dayatma değil. Küçük bant için kısa: 2-4 yaşta dikkat süresi
    /// zaten on beş dakikanın altında ve bu banttaki oyunlar da kısa. Meşe
    /// bandının turları (Yapboz'un on altı parçası, Sırala'nın ardışık
    /// miktarları) tek başına birkaç dakika sürüyor, yani aynı sınır orada
    /// üç dört tur ederdi.
    /// </remarks>
    public static readonly BandValue<int> SuggestedMinutes = new(15, 20, 30);

    /// <summary>Ebeveyn ekranındaki seçenekler, dakika. Sınırsız ayrıca duruyor.</summary>
    /// <remarks>
    /// Serbest sayı girişi değil hazır seçenekler: ebeveyn "kaç dakika doğru"
    /// sorusunu cevaplamak zorunda kalmıyor, listeden biri yeterli. Aralık
    /// üstte seyrekleşiyor (30'dan sonra 45 ve 60) çünkü o bölgede beş
    /// dakikalık fark bir şey ifade etmiyor.
    /// </remarks>
    public static readonly IReadOnlyList<int> Choices = [10, 15, 20, 30, 45, 60];

    /// <summary>
    /// Bugünün durumunu hesaplar.
    /// </summary>
    /// <param name="limitMinutes">
    /// Günlük sınır, dakika. <see cref="Unlimited"/> ya da negatifse sınır yok.
    /// </param>
    /// <param name="rounds">
    /// Profilin turları. Hangi güne ait olduğuna burada bakılıyor, çağıranın
    /// önceden süzmesi gerekmiyor.
    /// </param>
    /// <param name="today">Bugünün yerel tarihi.</param>
    public static ScreenTimeStatus Evaluate(
        int limitMinutes, IEnumerable<PlayedRound> rounds, DateOnly today)
    {
        var limit = limitMinutes > 0
            ? TimeSpan.FromMinutes(limitMinutes)
            : TimeSpan.Zero;

        var used = UsedOn(rounds, today);

        return new ScreenTimeStatus(limit, used);
    }

    /// <summary>Verilen gündeki toplam oyun süresi, turlar kırpılmış hâlde.</summary>
    public static TimeSpan UsedOn(IEnumerable<PlayedRound> rounds, DateOnly day)
    {
        var ticks = 0L;

        foreach (var round in rounds)
        {
            if (round.PlayedOn != day)
            {
                continue;
            }

            var duration = round.Duration < TimeSpan.Zero
                ? TimeSpan.Zero
                : round.Duration > PlayReport.LongestCountedRound
                    ? PlayReport.LongestCountedRound
                    : round.Duration;

            ticks += duration.Ticks;
        }

        return TimeSpan.FromTicks(ticks);
    }
}
