namespace Ploofy.Engine.Access;

/// <summary>
/// Ebeveyn kilidinin arkasına konması zorunlu olan işlemler.
/// </summary>
/// <remarks>
/// Hedef yaş grubunun (2-9) tek başına geçemeyeceği bir engel; amaç güvenlik
/// değil, yanlışlıkla satın alma ve uygulamadan çıkmayı önlemek. Play Families
/// ve App Store Kids kategorisi bunu pratikte zorunlu tutuyor.
/// </remarks>
public enum ParentalGateReason
{
    /// <summary>Abonelik satın alma / yönetme.</summary>
    Purchase,

    /// <summary>Ayarlar ekranı (bant değiştirme, dil, ses).</summary>
    Settings,

    /// <summary>
    /// Uygulamadan dışarı çıkan herhangi bir bağlantı (gizlilik politikası,
    /// destek, mağaza sayfası).
    /// </summary>
    ExternalLink,

    /// <summary>Çocuk profili ekleme / silme.</summary>
    ProfileManagement,
}

/// <summary>
/// Ebeveyne sorulan tek soruluk aritmetik engel.
/// </summary>
/// <remarks>
/// Zorluk kasıtlı olarak Meşe bandının (6-9 yaş) üstünde: iki basamaklı çarpma
/// + toplama, kafadan hızlıca yapılamayacak ama yetişkin için gündelik.
/// Sorunun metni yok, yalnızca sayılar — arayüz katmanı biçimlendirir, çeviri
/// gerekmez.
/// </remarks>
public sealed record ParentalGateChallenge(int Left, int Right, int Addend)
{
    /// <summary>Rastgele bir soru üretir. <paramref name="random"/> testlerde sabitlenebilir.</summary>
    public static ParentalGateChallenge Generate(Random? random = null)
    {
        var rng = random ?? Random.Shared;
        return new ParentalGateChallenge(
            // 6-9 arası çarpanlar: çarpım tablosunun en geç öğrenilen kısmı.
            Left: rng.Next(6, 10),
            Right: rng.Next(6, 10),
            // İki basamaklı ek terim, elde ile toplama gerektirsin diye.
            Addend: rng.Next(11, 30));
    }

    public int Answer => (Left * Right) + Addend;

    public bool Accepts(string? input) =>
        int.TryParse(input?.Trim(), out var parsed) && parsed == Answer;
}

/// <summary>
/// Kilidin açık olup olmadığını tutan durum nesnesi.
/// </summary>
/// <remarks>
/// Kalıcı değil: uygulama kapanınca kilit yeniden kapanır.
/// </remarks>
public sealed class ParentalGateState(TimeProvider? timeProvider = null)
{
    /// <summary>
    /// Kilidin bir kez geçildikten sonra açık kaldığı süre.
    /// </summary>
    /// <remarks>
    /// Ebeveynin ayarlarda gezinirken her ekranda yeniden soru çözmesi
    /// anlamsız; ama süre uzun olursa çocuk cihazı geri aldığında kilit hâlâ
    /// açık olur. Beş dakika bu ikisinin arasındaki denge.
    /// </remarks>
    public static readonly TimeSpan Grace = TimeSpan.FromMinutes(5);

    private readonly TimeProvider _time = timeProvider ?? TimeProvider.System;

    private DateTimeOffset? _unlockedAt;

    public bool IsUnlocked =>
        _unlockedAt is { } at && _time.GetUtcNow() - at < Grace;

    public void MarkUnlocked() => _unlockedAt = _time.GetUtcNow();

    public void Lock() => _unlockedAt = null;
}
