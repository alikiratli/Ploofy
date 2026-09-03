namespace Ploofy.Engine.Progress;

/// <summary>
/// Bir ödül merdiveninde nerede olunduğu.
/// </summary>
/// <param name="TotalStars">Profilin bütün bantlardaki toplam yıldızı.</param>
/// <param name="Unlocked">Açılmış ödül sayısı.</param>
/// <param name="Total">Merdivendeki toplam ödül sayısı.</param>
/// <param name="NextRequiredStars">
/// Sıradaki ödülün istediği yıldız; hepsi açıldıysa <c>null</c>.
/// </param>
public readonly record struct RewardProgress(
    int TotalStars,
    int Unlocked,
    int Total,
    int? NextRequiredStars)
{
    /// <summary>Hepsi açıldı mı?</summary>
    public bool IsComplete => Unlocked >= Total;

    /// <summary>Sıradaki ödüle kaç yıldız kaldı; hepsi açıldıysa sıfır.</summary>
    public int StarsToNext =>
        NextRequiredStars is { } next ? Math.Max(0, next - TotalStars) : 0;

    /// <summary>
    /// Sıradaki ödüle doğru dolan çubuk, 0-1.
    /// </summary>
    /// <remarks>
    /// Bir öncekiyle sıradaki eşiğin <b>arasında</b> ölçülüyor, sıfırdan
    /// değil. Sıfırdan ölçülseydi çubuk hep neredeyse dolu görünürdü:
    /// otuzuncu yıldızda otuz üçe giden çubuk %91 olurdu ve çocuk hiç
    /// ilerlemediğini görürdü.
    /// </remarks>
    public double FractionToNext
    {
        get
        {
            if (NextRequiredStars is not { } next)
            {
                return 1.0;
            }

            var previous = RewardLadder.RequiredStars(Unlocked);
            var span = next - previous;

            return span <= 0 ? 1.0 : Math.Clamp((TotalStars - previous) / (double)span, 0, 1);
        }
    }
}

/// <summary>
/// Toplam yıldızı ödüle çeviren merdiven.
/// </summary>
/// <remarks>
/// <para>
/// Yıldızlar bu oyundan önce hiçbir şey açmıyordu: birikiyor, ana ekranda
/// bir sayı olarak duruyor ve orada kalıyordu. Toplama karşılık vermek
/// motivasyon döngüsünü kapatan en küçük iş.
/// </para>
/// <para>
/// Ölçü <b>toplam</b> yıldız — oyun başına değil, rozet üzerinden değil.
/// Sebebi yaş: dört yaşındaki bir çocuğun takip edebileceği tek kural
/// "yıldız topla, yeni arkadaş gelsin". Oyun başına kilit her oyunu ayrı
/// ayrı üç yıldıza kadar zorlamayı gerektirirdi; rozet ise araya ikinci bir
/// katman koyup kuralı "rozet kazan, rozet avatarı açsın" hâline getirirdi.
/// </para>
/// <para>
/// Toplam yıldızın ikinci bir faydası var: <c>ProgressRepository</c> zaten
/// hesaplıyor ve bantlar arası korunuyor, yani bu merdiven <b>hiçbir yeni
/// tablo</b> istemiyor. Açılmış ödül her zaman toplamdan türetiliyor;
/// saklanan bir kilit listesi olmadığı için de bozulamıyor.
/// </para>
/// </remarks>
public static class RewardLadder
{
    /// <summary>
    /// İki ödül arasındaki yıldız aralığı.
    /// </summary>
    /// <remarks>
    /// Üç, bir turdan alınabilecek en yüksek yıldız. Yani ilk ödül tam bir
    /// tur sonunda geliyor — çocuk kuralı anlatılmadan, ilk oyununda
    /// görüyor. Aralık ikiye indirilse ödüller iki günde biterdi, dörde
    /// çıkarılsa ilk ödül ikinci turu beklerdi ve bağ kurulmazdı.
    /// </remarks>
    public const int StarsPerUnlock = 3;

    /// <summary>
    /// Sıradan <paramref name="index"/> olan ödülün istediği yıldız (1'den başlar).
    /// </summary>
    /// <remarks>
    /// Sıfır ve altı sıfır dönüyor: "sıfırıncı ödül" merdivenin başlangıç
    /// noktası, <see cref="RewardProgress.FractionToNext"/> onu bir alt sınır
    /// olarak kullanıyor.
    /// </remarks>
    public static int RequiredStars(int index) =>
        index <= 0 ? 0 : index * StarsPerUnlock;

    /// <summary>
    /// Toplam yıldızla açılmış ödül sayısı.
    /// </summary>
    /// <param name="totalStars">Negatif değerler sıfır sayılıyor.</param>
    /// <param name="rewardCount">Merdivendeki ödül sayısı; sonuç bununla sınırlı.</param>
    public static int UnlockedCount(int totalStars, int rewardCount)
    {
        if (totalStars < StarsPerUnlock || rewardCount <= 0)
        {
            return 0;
        }

        return Math.Min(totalStars / StarsPerUnlock, rewardCount);
    }

    /// <summary>Merdivenin o andaki durumu.</summary>
    public static RewardProgress Evaluate(int totalStars, int rewardCount)
    {
        var total = Math.Max(0, rewardCount);
        var stars = Math.Max(0, totalStars);
        var unlocked = UnlockedCount(stars, total);

        return new RewardProgress(
            stars,
            unlocked,
            total,
            unlocked >= total ? null : RequiredStars(unlocked + 1));
    }
}
