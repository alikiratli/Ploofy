namespace Ploofy.Engine.Sessions;

/// <summary>Oturumun kaç kişiyle ve hangi yolla oynandığı.</summary>
public enum SessionMode
{
    /// <summary>Tek çocuk, sıra yok.</summary>
    Solo,

    /// <summary>
    /// Aynı cihazı sırayla kullanan iki ya da daha fazla çocuk.
    /// İnternet, hesap ya da eşleşme gerektirmez.
    /// </summary>
    PassAndPlay,

    /// <summary>
    /// Aynı Wi-Fi / yerel ağdaki cihazla QR ya da yakındaki cihaz keşfiyle
    /// eşleşme. İnternetten yabancı bulma yok — yalnızca fiziksel olarak aynı
    /// odadaki biri. Faz 2'de uygulanacak; taşıma soyutlaması bugünden hazır.
    /// </summary>
    LocalNetwork,

    /// <summary>
    /// Ebeveyn onaylı aile bağlantısı: iki ebeveynin karşılıklı onayladığı
    /// cihazlar arasında (ör. anneanne ile torun). Faz 3.
    /// </summary>
    FamilyLink,
}

public static class SessionModeExtensions
{
    /// <summary>
    /// Bu sürümde çalışıyor mu? Arayüz henüz gelmemiş modları "yakında" olarak
    /// gösterir; <see cref="GameSession"/> bunları başlatmayı reddeder.
    /// </summary>
    public static bool IsImplemented(this SessionMode mode) =>
        mode is SessionMode.Solo or SessionMode.PassAndPlay;

    public static bool IsRemote(this SessionMode mode) =>
        mode is SessionMode.LocalNetwork or SessionMode.FamilyLink;
}

/// <summary>
/// Tek bir oynanışın değişmeyen kurulumu.
/// </summary>
/// <remarks>
/// Oyun sırasında değişen her şey (sıra, puan, faz)
/// <see cref="TurnController"/> içinde; burada yalnızca oturum başlarken
/// belirlenen ve sonuna kadar sabit kalan bilgiler var.
/// </remarks>
public sealed class GameSession
{
    /// <param name="roundsPerPlayer">
    /// Her oyuncunun kaç tur oynayacağı. Sıralı oyunda tur sayısı eşit olmak
    /// zorunda — bir çocuğun diğerinden az oynaması en hızlı kavga sebebi.
    /// </param>
    public GameSession(
        string gameId,
        SessionMode mode,
        IReadOnlyList<Player> players,
        int roundsPerPlayer = 1)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(gameId);
        ArgumentOutOfRangeException.ThrowIfLessThan(roundsPerPlayer, 1);

        if (players.Count == 0)
        {
            throw new ArgumentException("Oturumda en az bir oyuncu olmalı.", nameof(players));
        }

        if (mode == SessionMode.Solo && players.Count != 1)
        {
            throw new ArgumentException(
                "Tek kişilik oturumda tek oyuncu olur.", nameof(players));
        }

        if (players.Select(p => p.ProfileId).Distinct().Count() != players.Count)
        {
            throw new ArgumentException(
                "Aynı profil oturumda iki kez yer alamaz.", nameof(players));
        }

        if (!mode.IsImplemented())
        {
            throw new NotSupportedException($"{mode} bu sürümde henüz desteklenmiyor.");
        }

        GameId = gameId;
        Mode = mode;
        Players = players;
        RoundsPerPlayer = roundsPerPlayer;
    }

    public static GameSession Solo(string gameId, Player player) =>
        new(gameId, SessionMode.Solo, [player]);

    public string GameId { get; }

    public SessionMode Mode { get; }

    /// <summary>Sıra bu listedeki sırayla döner.</summary>
    public IReadOnlyList<Player> Players { get; }

    public int RoundsPerPlayer { get; }

    public int TotalTurns => Players.Count * RoundsPerPlayer;

    public bool IsMultiplayer => Players.Count > 1;

    public Player PlayerById(int profileId) =>
        Players.First(p => p.ProfileId == profileId);
}
