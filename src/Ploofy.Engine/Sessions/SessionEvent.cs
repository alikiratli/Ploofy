namespace Ploofy.Engine.Sessions;

/// <summary>
/// Oturum boyunca oyuncular arasında dolaşan olay.
/// </summary>
/// <remarks>
/// Sıralı oyunda bu olaylar cihazın içinde kalır; ileride yerel ağ eşleşmesi
/// eklendiğinde aynı olaylar tel üzerinden gidecek. Bu yüzden olaylar baştan
/// serileştirilebilir tutuluyor: yalnızca ilkel tipler ve sözlük.
/// </remarks>
public abstract record SessionEvent(int PlayerId)
{
    /// <summary>Tel üzerindeki ayrım anahtarı. Değişmez.</summary>
    public abstract string Type { get; }

    public abstract IReadOnlyDictionary<string, object?> ToPayload();
}

/// <summary>Sıra bu oyuncuya geçti.</summary>
public sealed record TurnStarted(int PlayerId, int TurnIndex) : SessionEvent(PlayerId)
{
    public override string Type => "turn_started";

    public override IReadOnlyDictionary<string, object?> ToPayload() =>
        new Dictionary<string, object?>
        {
            ["type"] = Type,
            ["playerId"] = PlayerId,
            ["turnIndex"] = TurnIndex,
        };
}

/// <summary>Oyuncu turunu bitirdi; <paramref name="Score"/> o turda topladığı ham puan.</summary>
public sealed record TurnFinished(int PlayerId, int TurnIndex, int Score) : SessionEvent(PlayerId)
{
    public override string Type => "turn_finished";

    public override IReadOnlyDictionary<string, object?> ToPayload() =>
        new Dictionary<string, object?>
        {
            ["type"] = Type,
            ["playerId"] = PlayerId,
            ["turnIndex"] = TurnIndex,
            ["score"] = Score,
        };
}

/// <summary>
/// Oyunun kendi ürettiği, motoru ilgilendirmeyen hamle (kart çevrildi, parça
/// yerleştirildi). Motor içeriğe bakmaz, yalnızca taşır.
/// </summary>
public sealed record GameMove(int PlayerId, IReadOnlyDictionary<string, object?> Move)
    : SessionEvent(PlayerId)
{
    public override string Type => "move";

    public override IReadOnlyDictionary<string, object?> ToPayload() =>
        new Dictionary<string, object?>
        {
            ["type"] = Type,
            ["playerId"] = PlayerId,
            ["move"] = Move,
        };
}
