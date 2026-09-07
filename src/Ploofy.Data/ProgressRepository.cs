using Ploofy.Engine;
using Ploofy.Engine.Catalog;
using Ploofy.Engine.Difficulty;
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

    /// <summary>
    /// Ödenmiş dönemin son günü (yyyy-MM-dd). Bu da yalnızca önbellek:
    /// ekranın "şu tarihte yenilenir / şu tarihte kapanır" diyebilmesi için
    /// var, erişim kararına girmiyor.
    /// </summary>
    public const string SubscriptionPeriodEnd = "subscription_period_end";

    /// <summary>
    /// Ödül kutlamasının en son hangi yıldız sayısında yapıldığı, profil başına.
    /// </summary>
    /// <remarks>
    /// Açılmış avatarın kendisi saklanmıyor — o her zaman toplam yıldızdan
    /// türetiliyor. Saklanan tek şey <b>gösterilen</b> sınır: tur sonu ekranı
    /// bu işaretle o anki toplamı karşılaştırıp aradaki ödülleri kutluyor,
    /// sonra işareti ileri alıyor. Böylece uygulama kutlama anında kapansa
    /// bile ödül kaybolmuyor, ikinci kez de kutlanmıyor.
    /// </remarks>
    public static string RewardsSeen(int profileId) => $"rewards_seen:{profileId}";

    /// <summary>
    /// Günlük oyun süresi sınırı, dakika; profil başına.
    /// </summary>
    /// <remarks>
    /// Anahtar yoksa sınır da yok. Varsayılanın "kapalı" olması şart:
    /// açık gelseydi güncellemeden sonra bütün çocuklar birden kilitlenir ve
    /// kimse sebebini bilmezdi. Bkz. <c>ScreenTimeBudget</c>.
    /// </remarks>
    public static string ScreenTimeLimit(int profileId) => $"screen_time:{profileId}";

    /// <summary>
    /// Bant içi uyarlama açık mı; profil başına.
    /// </summary>
    /// <remarks>
    /// Oyun süresi sınırının tersine varsayılanı <b>açık</b>. Sebep farkı
    /// şurada: sınır kapalıyken açılırsa çocuk birden kilitleniyor, uyarlama
    /// ise yalnızca zaten ustalaşılmış tek bir oyunu bir kademe zorlaştırıyor
    /// ve her yerde görünür duruyor. Anahtar yalnızca ebeveyn kapattığında
    /// yazılıyor. Bkz. <c>AdaptiveDifficulty</c>.
    /// </remarks>
    public static string AdaptiveDifficulty(int profileId) => $"adaptive:{profileId}";
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
        await _database.Connection.Table<RoundHistoryRow>()
            .DeleteAsync(r => r.ProfileId == id);
        await _database.Connection.Table<ChildProfileRow>()
            .DeleteAsync(r => r.Id == id);

        // Ödül işareti ayarlar tablosunda, profil başına bir anahtar. Silinmese
        // aynı id'yi alan bir sonraki profil, hiç oynamadan "kutlanmış" başlardı.
        // Anahtar önce değişkene alınıyor: sqlite-net sorgu ağacında metot
        // çağrısı çeviremiyor, yakalanmış bir yereli sabit olarak alıyor.
        var rewardKey = SettingKeys.RewardsSeen(id);
        await _database.Connection.Table<AppSettingRow>()
            .DeleteAsync(r => r.Key == rewardKey);

        var screenTimeKey = SettingKeys.ScreenTimeLimit(id);
        await _database.Connection.Table<AppSettingRow>()
            .DeleteAsync(r => r.Key == screenTimeKey);

        var adaptiveKey = SettingKeys.AdaptiveDifficulty(id);
        await _database.Connection.Table<AppSettingRow>()
            .DeleteAsync(r => r.Key == adaptiveKey);
    }

    /// <summary>Profili oyun oturumunda kullanılan oyuncuya çevirir.</summary>
    /// <remarks>
    /// <b>Kademesiz.</b> Oturum kuran her yer <see cref="ToPlayerAsync"/>
    /// kullanmalı: bu aşırı yüklemeyle kurulan bir oyuncu bant içi uyarlamayı
    /// sessizce devre dışı bırakır ve hata hiçbir yerde görünmez.
    /// </remarks>
    public static Player ToPlayer(ChildProfileRow profile) => new(
        profile.Id,
        profile.DisplayName,
        AgeBandExtensions.FromId(profile.AgeBandId),
        profile.AvatarId);

    /// <summary>Profili, <b>o oyundaki</b> kademesiyle birlikte oyuncuya çevirir.</summary>
    public async Task<Player> ToPlayerAsync(ChildProfileRow profile, string gameId)
    {
        var player = ToPlayer(profile);
        return player with { Step = await StepForAsync(player.ProfileId, gameId, player.Band) };
    }

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

        // Geçmiş ayrı bir satır: yukarıdaki tablo "en iyisi ne" diyor, bu
        // "ne zaman ne oldu". Ebeveyn raporundaki eğilim yalnızca buradan
        // çıkabiliyor.
        await _database.Connection.InsertAsync(new RoundHistoryRow
        {
            ProfileId = outcome.ProfileId,
            GameId = outcome.GameId,
            AgeBandId = bandId,
            Stars = stars,
            Score = score,
            Mistakes = outcome.Mistakes,
            DurationSeconds = outcome.Elapsed.TotalSeconds,
            PlayedAtLocal = DateTime.Now,
        });

        return stars;
    }

    /// <summary>
    /// Bir profilin oynanmış turları, yeniden eskiye.
    /// </summary>
    /// <param name="since">
    /// Bu <b>yerel</b> günden itibaren. Rapor kaç günü kapsıyorsa o kadarı
    /// okunuyor; bütün geçmişi belleğe almanın bir sebebi yok.
    /// </param>
    public async Task<IReadOnlyList<RoundHistoryRow>> HistorySinceAsync(
        int profileId, DateOnly since)
    {
        await _database.InitializeAsync();

        var from = since.ToDateTime(TimeOnly.MinValue);

        return await _database.Connection.Table<RoundHistoryRow>()
            .Where(r => r.ProfileId == profileId && r.PlayedAtLocal >= from)
            .OrderByDescending(r => r.PlayedAtLocal)
            .ToListAsync();
    }

    /// <summary>
    /// Kayıt satırını motorun gördüğü tura çevirir.
    /// </summary>
    /// <remarks>
    /// Motor SQLite bilmiyor. Dönüştürme burada, tek yerde: rapor ve oyun
    /// süresi bütçesi aynı satırları okuyor ve iki ayrı dönüştürme, ebeveyne
    /// birbirini tutmayan iki rakam gösterme riski demek.
    /// </remarks>
    public static PlayedRound ToPlayedRound(RoundHistoryRow row) => new(
        DateOnly.FromDateTime(row.PlayedAtLocal),
        row.GameId,
        AgeBandExtensions.FromId(row.AgeBandId),
        row.Stars,
        row.Mistakes,
        TimeSpan.FromSeconds(row.DurationSeconds));

    // --- Oyun süresi sınırı ---

    /// <summary>Profilin günlük sınırı, dakika. Sınır yoksa sıfır.</summary>
    public async Task<int> ScreenTimeLimitAsync(int profileId)
    {
        var raw = await GetSettingAsync(SettingKeys.ScreenTimeLimit(profileId));
        return raw is not null && int.TryParse(raw, out var minutes) && minutes > 0
            ? minutes
            : ScreenTimeBudget.Unlimited;
    }

    /// <summary>Sınırı yazar. <see cref="ScreenTimeBudget.Unlimited"/> sınırı kaldırır.</summary>
    public Task SetScreenTimeLimitAsync(int profileId, int minutes) =>
        SetSettingAsync(
            SettingKeys.ScreenTimeLimit(profileId),
            Math.Max(0, minutes).ToString());

    /// <summary>
    /// Profilin bugünkü bütçe durumu.
    /// </summary>
    /// <remarks>
    /// Yalnızca bugünün satırları okunuyor. Gün <b>yerel</b> saatle dönüyor:
    /// gece 22:00'de oynanan bir tur ebeveyn için bugün, UTC'de yarın olurdu.
    /// </remarks>
    public async Task<ScreenTimeStatus> ScreenTimeTodayAsync(int profileId)
    {
        var today = DateOnly.FromDateTime(DateTime.Now);
        var limit = await ScreenTimeLimitAsync(profileId);

        // Sınır yoksa geçmişi hiç okumuyoruz: her oyun açılışında yapılan bir
        // sorgu, sınır kullanmayan ailelerde bedavaya çalışırdı.
        if (limit <= ScreenTimeBudget.Unlimited)
        {
            return ScreenTimeBudget.Evaluate(ScreenTimeBudget.Unlimited, [], today);
        }

        var history = await HistorySinceAsync(profileId, today);
        return ScreenTimeBudget.Evaluate(limit, history.Select(ToPlayedRound), today);
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

    /// <summary>
    /// Bu profil için en son kutlanan yıldız sayısı.
    /// </summary>
    /// <param name="currentTotal">
    /// O anki toplam. İşaret hiç yazılmamışsa buraya kurulup geri dönüyor —
    /// yani zaten yıldız biriktirmiş <b>eski</b> bir profil, güncellemeden
    /// sonra on üç kutlamayı arka arkaya görmüyor.
    /// </param>
    public async Task<int> RewardWatermarkAsync(int profileId, int currentTotal)
    {
        var raw = await GetSettingAsync(SettingKeys.RewardsSeen(profileId));
        if (raw is not null && int.TryParse(raw, out var seen))
        {
            return seen;
        }

        await SetRewardWatermarkAsync(profileId, currentTotal);
        return currentTotal;
    }

    /// <summary>İşareti ileri alır. Geri alınmıyor: kutlanan kutlanmış sayılır.</summary>
    public async Task SetRewardWatermarkAsync(int profileId, int stars)
    {
        var key = SettingKeys.RewardsSeen(profileId);
        var raw = await GetSettingAsync(key);
        var current = raw is not null && int.TryParse(raw, out var seen) ? seen : 0;

        if (stars > current || raw is null)
        {
            await SetSettingAsync(key, stars.ToString());
        }
    }

    // --- Bant içi uyarlama ---

    /// <summary>Ebeveyn uyarlamayı açık bırakmış mı? Varsayılan açık.</summary>
    public Task<bool> AdaptiveDifficultyEnabledAsync(int profileId) =>
        GetBoolSettingAsync(SettingKeys.AdaptiveDifficulty(profileId), orElse: true);

    public Task SetAdaptiveDifficultyAsync(int profileId, bool enabled) =>
        SetBoolSettingAsync(SettingKeys.AdaptiveDifficulty(profileId), enabled);

    /// <summary>
    /// Bir oyunun son turları, yeniden eskiye.
    /// </summary>
    /// <remarks>
    /// Kademe kuralının gördüğü tek şey bu. Tarihe göre değil sayıya göre
    /// süzülüyor: bir ay ara verip dönen çocuk, bıraktığı yerde devam ediyor.
    /// </remarks>
    public async Task<IReadOnlyList<RoundHistoryRow>> RecentRoundsAsync(
        int profileId, string gameId, AgeBand band, int count)
    {
        await _database.InitializeAsync();

        var bandId = band.ToId();

        return await _database.Connection.Table<RoundHistoryRow>()
            .Where(r => r.ProfileId == profileId
                && r.GameId == gameId
                && r.AgeBandId == bandId)
            .OrderByDescending(r => r.Id)
            .Take(count)
            .ToListAsync();
    }

    /// <summary>
    /// Bir oyunun bu profildeki güncel kademesi.
    /// </summary>
    /// <remarks>
    /// Ebeveyn uyarlamayı kapattıysa geçmişe hiç gidilmiyor; kapalıyken her
    /// oyun açılışında yapılan bir sorgu bedavaya çalışırdı.
    /// </remarks>
    public async Task<DifficultyStep> StepForAsync(int profileId, string gameId, AgeBand band)
    {
        if (!AdaptiveDifficulty.CanStretch(band)
            || !await AdaptiveDifficultyEnabledAsync(profileId))
        {
            return DifficultyStep.Base;
        }

        var recent = await RecentRoundsAsync(
            profileId, gameId, band, AdaptiveDifficulty.PerfectRoundsToStretch);

        return AdaptiveDifficulty.Evaluate(band, recent.Select(ToPlayedRound));
    }

    /// <summary>
    /// Bantta oynanabilen bütün oyunların kademesi — ana ekranın ve ebeveyn
    /// raporunun işareti buradan.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Oyun başına bir sorgu. Tek sorguya sığdırmak, "her oyunun son üç turu"nu
    /// pencereli bir okumaya çevirirdi; o pencerenin dışında kalan bir oyunda
    /// ana ekranın işareti oyunun gerçek kademesini tutmazdı.
    /// </para>
    /// <para>
    /// Yeterince oynanmamış oyunlar sorulmadan eleniyor. Eleme kuralı elemiyor,
    /// yalnızca zaten <see cref="DifficultyStep.Base"/> çıkacak sorguları
    /// atıyor: kademe için o bantta en az üç tur gerekiyor ve tur sayısı
    /// ilerleme satırında zaten duruyor. Elemesiz hâli, hiç oynanmamış her
    /// oyun için bütün geçmişi baştan sona geziyordu — ana ekran her açıldığında.
    /// </para>
    /// </remarks>
    public async Task<IReadOnlyDictionary<string, DifficultyStep>> StepsForAsync(
        int profileId, AgeBand band)
    {
        var steps = new Dictionary<string, DifficultyStep>(StringComparer.Ordinal);

        if (!AdaptiveDifficulty.CanStretch(band)
            || !await AdaptiveDifficultyEnabledAsync(profileId))
        {
            return steps;
        }

        var bandId = band.ToId();
        var played = (await ProgressForAsync(profileId))
            .Where(r => r.AgeBandId == bandId
                && r.PlayCount >= AdaptiveDifficulty.PerfectRoundsToStretch)
            .Select(r => r.GameId)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var game in GameCatalog.ForBand(band))
        {
            if (!played.Contains(game.Id))
            {
                continue;
            }

            var recent = await RecentRoundsAsync(
                profileId, game.Id, band, AdaptiveDifficulty.PerfectRoundsToStretch);

            var step = AdaptiveDifficulty.Evaluate(band, recent.Select(ToPlayedRound));
            if (step != DifficultyStep.Base)
            {
                steps[game.Id] = step;
            }
        }

        return steps;
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
