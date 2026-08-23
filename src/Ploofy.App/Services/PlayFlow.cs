using Ploofy.Engine.Sessions;

namespace Ploofy.App.Services;

/// <summary>Sonuç ekranında gösterilen tek oyuncunun satırı.</summary>
public sealed record PlayerResult(string DisplayName, string AvatarId, int Stars, int Score);

/// <summary>Biten bir oynanışın özeti.</summary>
public sealed record RoundSummary(
    string GameId,
    IReadOnlyList<PlayerResult> Players,
    bool IsMultiplayer)
{
    /// <summary>Puanı en yüksek oyuncular. Beraberlikte birden fazla olabilir.</summary>
    public IReadOnlyList<PlayerResult> Winners
    {
        get
        {
            if (!IsMultiplayer || Players.Count == 0)
            {
                return [];
            }

            var best = Players.Max(p => p.Score);
            return Players.Where(p => p.Score == best).ToList();
        }
    }

    /// <summary>Beraberlik mi? "İkiniz de kazandınız" bu durumda gösteriliyor.</summary>
    public bool IsDraw => IsMultiplayer && Winners.Count > 1;
}

/// <summary>
/// Oyun akışının sayfalar arasında taşıdığı durum.
/// </summary>
/// <remarks>
/// Kabuk gezinme parametreleri yalnızca metin taşıyor; oturum ve sonuç
/// nesnelerini metne çevirip geri kurmak yerine tek bir yerde tutuluyor.
/// Akış zaten tek yönlü: oyun seç → kurulum → oyna → sonuç.
/// </remarks>
public sealed class PlayFlow
{
    /// <summary>Ana ekranda seçilen oyun.</summary>
    public string? SelectedGameId { get; set; }

    /// <summary>Kurulum ekranında hazırlanan oturum; oyun sayfası bunu alır.</summary>
    public GameSession? PendingSession { get; set; }

    /// <summary>Biten oynanışın özeti; sonuç ekranı bunu gösterir.</summary>
    public RoundSummary? LastSummary { get; set; }

    public void Clear()
    {
        SelectedGameId = null;
        PendingSession = null;
        LastSummary = null;
    }
}
