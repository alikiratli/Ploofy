using Ploofy.Engine;
using Ploofy.Engine.Progress;
using Ploofy.Engine.Sessions;

namespace Ploofy.Data;

/// <summary>
/// Ayarların sabit anahtarları.
/// </summary>
/// <remarks>
/// Serbest metin yerine burada toplanıyor ki yazım hatası yüzünden sessizce
/// kaybolan bir ayar olmasın.
/// </remarks>
public static class SettingKeys
{
    /// <summary>Uygulama açılışında seçili gelen profil.</summary>
    public const string ActiveProfile = "active_profile_id";

    /// <summary>Ebeveynin seçtiği arayüz dili (tr/en/de). Boşsa cihaz dili kullanılır.</summary>
    public const string Locale = "locale";

    public const string SoundEnabled = "sound_enabled";

    public const string HapticsEnabled = "haptics_enabled";

    /// <summary>
    /// Mağazadan gelen son abonelik durumu. Yalnızca önbellek — doğruluk
    /// kaynağı her zaman mağazadır; uygulama çevrimdışıyken son bilinen değer
    /// kullanılır.
    /// </summary>
    public const string CachedSubscription = "cached_subscription_status";
}

/// <summary>
/// İlerleme verisinin tek giriş kapısı.
/// </summary>
/// <remarks>
/// Sayfalar ve görünüm modelleri tabloları doğrudan görmez; böylece depolama
/// değişse bile (ör. ileride yedekleme eklenirse) oyun ve arayüz kodu aynı kalır.
/// </remarks>
public sealed class ProgressRepository(ProgressDatabase database)
{
    private readonly ProgressDatabase _database = database;

    // --- Profiller ---

    public async Task<IReadOnlyList<ChildProfileRow>> ListProfilesAsync()
    {
        await _database.InitializeAsync();
        return await _database.Connection.Table<ChildProfileRow>().ToListAsync();
    }

    public async Task<ChildProfileRow?> ProfileByIdAsync(int id)
    {
        await _database.InitializeAsync();
        return await _database.Connection.Table<ChildProfileRow>()
            .Where(p => p.Id == id)
            .FirstOrDefaultAsync();
    }

    public async Task<ChildProfileRow> CreateProfileAsync(
        string displayName,
        AgeBand band,
        string avatarId,
        string themeId = "forest")
    {
        await _database.InitializeAsync();

        var row = new ChildProfileRow
        {
            DisplayName = displayName,
            AgeBandId = band.ToId(),
            AvatarId = avatarId,
            ThemeId = themeId,
            CreatedAtUtc = DateTime.UtcNow,
        };

        await _database.Connection.InsertAsync(row);
        return row;
    }

    public async Task UpdateProfileAsync(ChildProfileRow profile)
    {
        await _database.InitializeAsync();
        await _database.Connection.UpdateAsync(profile);
    }

    /// <summary>
    /// Profili ve ona bağlı bütün ilerlemeyi siler. Ebeveyn kilidi arkasında
    /// çağrılmalı — geri alınamaz.
    /// </summary>
    public async Task DeleteProfileAsync(int id)
    {
        await _database.InitializeAsync();

        // sqlite-net yabancı anahtar kısıtı kurmuyor; bağlı satırları elle
        // siliyoruz ki silinen profilin yıldızları veritabanında öksüz kalmasın.
        await _database.Connection.Table<GameProgressRow>()
            .DeleteAsync(r => r.ProfileId == id);
        await _database.Connection.Table<BadgeUnlockRow>()
            .DeleteAsync(r => r.ProfileId == id);
        await _database.Connection.Table<ChildProfileRow>()
            .DeleteAsync(r => r.Id == id);
    }

    /// <summary>Profili oyun oturumunda kullanılan oyuncuya çevirir.</summary>
    public static Player ToPlayer(ChildProfileRow profile) => new(
        profile.Id,
        profile.DisplayName,
        AgeBandExtensions.FromId(profile.AgeBandId),
        profile.AvatarId);

    // --- İlerleme ---

    /// <summary>
    /// Bir tur bittiğinde çağrılır ve kazanılan yıldızı döner.
    /// </summary>
    /// <remarks>
    /// Yıldız ve puan yalnızca daha iyisi geldiğinde güncellenir; kötü bir tur
    /// çocuğun daha önce kazandığını geri almaz.
    /// </remarks>
    public async Task<int> RecordRoundAsync(RoundOutcome outcome)
    {
        await _database.InitializeAsync();

        var stars = StarRating.ForOutcome(outcome);
        var score = StarRating.RawScore(outcome);
        var bandId = outcome.Band.ToId();
        var key = GameProgressRow.MakeKey(outcome.ProfileId, outcome.GameId, bandId);

        var existing = await _database.Connection.Table<GameProgressRow>()
            .Where(r => r.Key == key)
            .FirstOrDefaultAsync();

        if (existing is null)
        {
            await _database.Connection.InsertAsync(new GameProgressRow
            {
                Key = key,
                ProfileId = outcome.ProfileId,
                GameId = outcome.GameId,
                AgeBandId = bandId,
                BestStars = stars,
                BestScore = score,
                PlayCount = 1,
                LastPlayedAtUtc = DateTime.UtcNow,
            });
        }
        else
        {
            existing.BestStars = Math.Max(existing.BestStars, stars);
            existing.BestScore = Math.Max(existing.BestScore, score);
            existing.PlayCount++;
            existing.LastPlayedAtUtc = DateTime.UtcNow;
            await _database.Connection.UpdateAsync(existing);
        }

        return stars;
    }

    public async Task<IReadOnlyList<GameProgressRow>> ProgressForAsync(int profileId)
    {
        await _database.InitializeAsync();
        return await _database.Connection.Table<GameProgressRow>()
            .Where(r => r.ProfileId == profileId)
            .ToListAsync();
    }

    /// <summary>
    /// Koleksiyon ekranındaki toplam yıldız — bütün bantlardaki en iyi
    /// sonuçların toplamı. Bant değiştiren çocuk toplamını kaybetmez.
    /// </summary>
    public async Task<int> TotalStarsAsync(int profileId)
    {
        var rows = await ProgressForAsync(profileId);
        return rows.Sum(r => r.BestStars);
    }

    // --- Rozetler ---

    public async Task UnlockBadgeAsync(int profileId, string badgeId)
    {
        await _database.InitializeAsync();
        await _database.Connection.InsertOrReplaceAsync(new BadgeUnlockRow
        {
            Key = BadgeUnlockRow.MakeKey(profileId, badgeId),
            ProfileId = profileId,
            BadgeId = badgeId,
            UnlockedAtUtc = DateTime.UtcNow,
        });
    }

    public async Task<IReadOnlyList<BadgeUnlockRow>> BadgesForAsync(int profileId)
    {
        await _database.InitializeAsync();
        return await _database.Connection.Table<BadgeUnlockRow>()
            .Where(r => r.ProfileId == profileId)
            .ToListAsync();
    }

    // --- Ayarlar ---

    public async Task<string?> GetSettingAsync(string key)
    {
        await _database.InitializeAsync();
        var row = await _database.Connection.Table<AppSettingRow>()
            .Where(s => s.Key == key)
            .FirstOrDefaultAsync();
        return row?.Value;
    }

    public async Task SetSettingAsync(string key, string value)
    {
        await _database.InitializeAsync();
        await _database.Connection.InsertOrReplaceAsync(
            new AppSettingRow { Key = key, Value = value });
    }

    public async Task<bool> GetBoolSettingAsync(string key, bool orElse)
    {
        var raw = await GetSettingAsync(key);
        return raw is null ? orElse : raw == "true";
    }

    public Task SetBoolSettingAsync(string key, bool value) =>
        SetSettingAsync(key, value ? "true" : "false");
}
