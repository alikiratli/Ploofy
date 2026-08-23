namespace Ploofy.Engine.Sessions;

/// <summary>
/// Oturumdaki olayların gidip geldiği kanal.
/// </summary>
/// <remarks>
/// Bugün tek bir uygulaması var (<see cref="LocalTransport"/> — aynı cihaz,
/// sıralı oyun). Yerel ağ eşleşmesi ve ebeveyn onaylı aile bağlantısı bu
/// arayüzün arkasına takılacak; oyunların kodu değişmeyecek. Oyunlar taşıma
/// katmanını asla doğrudan tanımaz, yalnızca
/// <see cref="TurnController"/> üzerinden konuşur.
/// </remarks>
public interface ISessionTransport : IAsyncDisposable
{
    event EventHandler<SessionEvent>? EventReceived;

    ValueTask SendAsync(SessionEvent sessionEvent);

    /// <summary>
    /// Uzaktaki oyuncularla mı oynanıyor? Arayüz "bağlantı koptu" durumlarını
    /// yalnızca bu true iken düşünmek zorunda.
    /// </summary>
    bool IsRemote { get; }
}

/// <summary>
/// Aynı cihazda sırayla oynama (pass-and-play).
/// </summary>
/// <remarks>
/// Ağ yok, hesap yok, internet yok: olay yayınlandığı gibi geri döner. Uzak
/// taşımalarla aynı akışı kullanmak, çok oyunculu kod yolunun ilk günden
/// gerçek trafikle çalışmasını sağlıyor.
/// </remarks>
public sealed class LocalTransport : ISessionTransport
{
    private bool _disposed;

    public event EventHandler<SessionEvent>? EventReceived;

    public bool IsRemote => false;

    public ValueTask SendAsync(SessionEvent sessionEvent)
    {
        if (!_disposed)
        {
            EventReceived?.Invoke(this, sessionEvent);
        }

        return ValueTask.CompletedTask;
    }

    public ValueTask DisposeAsync()
    {
        _disposed = true;
        EventReceived = null;
        return ValueTask.CompletedTask;
    }
}
