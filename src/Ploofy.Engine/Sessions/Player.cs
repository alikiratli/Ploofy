using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Sessions;

/// <summary>
/// Bir oturumda yer alan oyuncu.
/// </summary>
/// <remarks>
/// Kalıcı çocuk profilinin (veritabanı) oturum içindeki karşılığı. Ayrı
/// tutulmasının sebebi: aynı cihazda sırayla oynarken iki farklı profil aynı
/// oyunda <b>farklı bantlarda</b> yer alabilir — küçük kardeş Filiz, büyük
/// kardeş Meşe olarak aynı turu oynar.
/// </remarks>
/// <param name="Band">
/// Bu oyuncunun kendi zorluk bandı. Oturum genelinde tek bir bant yok.
/// </param>
/// <param name="IsLocal">
/// Bu cihazda mı oturuyor? Sıralı oyunda hep true; ileride yerel ağ ya da aile
/// bağlantısı geldiğinde uzaktaki oyuncular için false olacak.
/// </param>
/// <param name="Step">
/// Bandın içindeki kademe — bu oyuncu <b>bu oyunda</b> nerede duruyor.
/// Oturum kurulurken bir kez hesaplanıyor ve tur boyunca değişmiyor: zorluk
/// oyunun ortasında kaymamalı. Bkz. <see cref="AdaptiveDifficulty"/>.
/// </param>
public sealed record Player(
    int ProfileId,
    string DisplayName,
    AgeBand Band,
    string AvatarId,
    bool IsLocal = true,
    DifficultyStep Step = DifficultyStep.Base)
{
    /// <summary>
    /// Oyunun zorluk tablosunda okunacak bant.
    /// </summary>
    /// <remarks>
    /// Oyunlar <c>ForBand</c> çağrısına bunu veriyor, <see cref="Band"/>'ı
    /// değil. Ayrım kasıtlı: kademe knob'ları kaydırıyor ama yıldız,
    /// <see cref="DifficultyProfile"/> ve kayıt hep gerçek bantla yürüyor.
    /// </remarks>
    public AgeBand KnobBand => AdaptiveDifficulty.KnobBand(Band, Step);

    /// <summary>Ekranda "bir kademe yukarı" işareti gösterilecek mi?</summary>
    public bool IsStretched => KnobBand != Band;
}
