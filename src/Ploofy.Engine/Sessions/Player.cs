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
public sealed record Player(
    int ProfileId,
    string DisplayName,
    AgeBand Band,
    string AvatarId,
    bool IsLocal = true);
