namespace Ploofy.Engine.Sessions;

/// <summary>Oturumun o anki durumu.</summary>
public enum TurnPhase
{
    /// <summary>Henüz başlamadı.</summary>
    Idle,

    /// <summary>
    /// Sıra devrediliyor — "cihazı kardeşine ver" ekranı. Bu ara adım olmadan
    /// sıralı oyunda çocuk, kardeşinin turunu yanlışlıkla oynuyor.
    /// </summary>
    Handoff,

    /// <summary>Sıradaki oyuncu oynuyor.</summary>
    Playing,

    /// <summary>Bütün turlar bitti.</summary>
    Finished,
}

/// <param name="TurnIndex">
/// Sıfırdan başlar, <see cref="GameSession.TotalTurns"/> değerine kadar gider.
/// </param>
/// <param name="CurrentPlayer">Oyun bittiğinde null.</param>
/// <param name="Scores">ProfileId -> toplam ham puan.</param>
public sealed record TurnState(
    TurnPhase Phase,
    int TurnIndex,
    Player? CurrentPlayer,
    IReadOnlyDictionary<int, int> Scores)
{
    /// <summary>
    /// En yüksek puandan başlayarak sıralanmış oyuncular.
    /// </summary>
    /// <remarks>
    /// Beraberlik bozulmaz: iki çocuk eşit bitirdiyse bu, arayüzde
    /// "ikiniz de kazandınız" olarak gösterilecek iyi bir sonuç.
    /// </remarks>
    public IReadOnlyList<KeyValuePair<int, int>> Standings =>
        Scores.OrderByDescending(e => e.Value).ToList();
}

/// <summary>
/// Sırayı, turları ve puanları yürüten tek yer.
/// </summary>
/// <remarks>
/// <para>
/// Mini oyunlar sırayı kendileri hesaplamaz: turu bitirdiklerinde
/// <see cref="FinishTurnAsync"/> çağırır, gerisini bu sınıf halleder. Aynı
/// sınıf tek kişilik oyunda da çalışır (tek oyuncu, tek tur) — böylece
/// oyunların içinde "çok oyunculu mu?" dallanması olmaz.
/// </para>
/// <para>
/// Olaylar <see cref="ISessionTransport"/> üzerinden akar. Bugün taşıma
/// cihazın içinde (<see cref="LocalTransport"/>); yerel ağ eklendiğinde
/// yalnızca taşıma değişecek.
/// </para>
/// </remarks>
public sealed class TurnController : IAsyncDisposable
{
    private readonly ISessionTransport _transport;

    public TurnController(GameSession session, ISessionTransport? transport = null)
    {
        Session = session;
        _transport = transport ?? new LocalTransport();
        _transport.EventReceived += OnTransportEvent;

        State = new TurnState(
            TurnPhase.Idle,
            TurnIndex: 0,
            CurrentPlayer: session.Players[0],
            Scores: session.Players.ToDictionary(p => p.ProfileId, _ => 0));
    }

    public GameSession Session { get; }

    public TurnState State { get; private set; }

    /// <summary>Durum her değiştiğinde tetiklenir; arayüz buna bağlanır.</summary>
    public event EventHandler<TurnState>? StateChanged;

    /// <summary>
    /// Taşımadan gelen ham olaylar. Bugün yalnızca kendi gönderdiklerimiz
    /// döner; uzak taşımada karşı taraftan gelenler de buradan akacak.
    /// </summary>
    public event EventHandler<SessionEvent>? EventReceived;

    /// <summary>
    /// Oturumu başlatır. Tek kişilik oyunda doğrudan oynamaya geçer; sıralı
    /// oyunda önce devir ekranı gösterilir.
    /// </summary>
    public ValueTask StartAsync()
    {
        if (State.Phase != TurnPhase.Idle)
        {
            return ValueTask.CompletedTask;
        }

        return BeginTurnAsync(0);
    }

    /// <summary>Devir ekranındaki "hazırım" dokunuşu.</summary>
    public async ValueTask ConfirmHandoffAsync()
    {
        if (State.Phase != TurnPhase.Handoff)
        {
            return;
        }

        var player = State.CurrentPlayer!;
        Emit(State with { Phase = TurnPhase.Playing });
        await _transport.SendAsync(new TurnStarted(player.ProfileId, State.TurnIndex));
    }

    /// <summary>Sıradaki oyuncu turunu bitirdi.</summary>
    public async ValueTask FinishTurnAsync(int score)
    {
        if (State.Phase != TurnPhase.Playing)
        {
            return;
        }

        var player = State.CurrentPlayer!;
        var turnIndex = State.TurnIndex;

        var scores = new Dictionary<int, int>(State.Scores);
        scores[player.ProfileId] = scores.GetValueOrDefault(player.ProfileId) + score;

        await _transport.SendAsync(new TurnFinished(player.ProfileId, turnIndex, score));

        var next = turnIndex + 1;
        if (next >= Session.TotalTurns)
        {
            Emit(new TurnState(TurnPhase.Finished, next, CurrentPlayer: null, scores));
            return;
        }

        Emit(State with { Scores = scores });
        await BeginTurnAsync(next);
    }

    /// <summary>
    /// Oyunun kendi hamlesini oturuma duyurur. Motor içeriğe bakmaz; bu yalnızca
    /// uzak oyuncuların aynı tahtayı görmesi için var (bugün yerel geri döngü).
    /// </summary>
    public ValueTask SendMoveAsync(IReadOnlyDictionary<string, object?> move)
    {
        var player = State.CurrentPlayer;
        return player is null
            ? ValueTask.CompletedTask
            : _transport.SendAsync(new GameMove(player.ProfileId, move));
    }

    private async ValueTask BeginTurnAsync(int turnIndex)
    {
        var player = Session.Players[turnIndex % Session.Players.Count];

        // Tek kişilik oyunda devir ekranı anlamsız — doğrudan oynamaya geçilir.
        if (!Session.IsMultiplayer)
        {
            Emit(new TurnState(TurnPhase.Playing, turnIndex, player, State.Scores));
            await _transport.SendAsync(new TurnStarted(player.ProfileId, turnIndex));
            return;
        }

        Emit(new TurnState(TurnPhase.Handoff, turnIndex, player, State.Scores));
    }

    private void Emit(TurnState next)
    {
        State = next;
        StateChanged?.Invoke(this, next);
    }

    private void OnTransportEvent(object? sender, SessionEvent e) =>
        EventReceived?.Invoke(this, e);

    public async ValueTask DisposeAsync()
    {
        _transport.EventReceived -= OnTransportEvent;
        StateChanged = null;
        EventReceived = null;
        await _transport.DisposeAsync();
    }
}
