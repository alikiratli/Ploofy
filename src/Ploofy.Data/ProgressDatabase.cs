using SQLite;

namespace Ploofy.Data;

/// <summary>
/// İlerleme veritabanının açılışı ve şeması.
/// </summary>
/// <remarks>
/// Dosya cihazın uygulama veri klasöründe durur; yedeklenmez, dışarı
/// gönderilmez. Uygulama tarafı yolu verir (MAUI'de
/// <c>FileSystem.AppDataDirectory</c>), testler geçici bir dosya ya da
/// <c>:memory:</c> verir — bu yüzden sınıf yolu kendisi hesaplamıyor.
/// </remarks>
public sealed class ProgressDatabase : IAsyncDisposable
{
    private readonly SQLiteAsyncConnection _connection;
    private Task? _initialization;

    public ProgressDatabase(string databasePath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(databasePath);

        // SharedCache: aynı bağlantı birden çok sayfadan eşzamanlı okunuyor.
        _connection = new SQLiteAsyncConnection(
            databasePath,
            SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.Create | SQLiteOpenFlags.SharedCache);
    }

    internal SQLiteAsyncConnection Connection => _connection;

    /// <summary>
    /// Tabloları oluşturur. Birden çok kez çağrılabilir; ilk çağrının görevi
    /// paylaşılır, böylece açılışta paralel istekler şemayı iki kez kurmaz.
    /// </summary>
    public Task InitializeAsync() => _initialization ??= CreateTablesAsync();

    private async Task CreateTablesAsync()
    {
        await _connection.CreateTableAsync<ChildProfileRow>();
        await _connection.CreateTableAsync<GameProgressRow>();
        await _connection.CreateTableAsync<BadgeUnlockRow>();
        await _connection.CreateTableAsync<RoundHistoryRow>();
        await _connection.CreateTableAsync<AppSettingRow>();
    }

    public async ValueTask DisposeAsync() => await _connection.CloseAsync();
}
