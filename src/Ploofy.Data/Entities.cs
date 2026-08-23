using SQLite;

namespace Ploofy.Data;

/// <summary>
/// Cihazdaki çocuk profili.
/// </summary>
/// <remarks>
/// Profil tamamen yereldir: sunucuya gitmez, yedeklenmez, bir hesaba bağlı
/// değildir. <see cref="DisplayName"/> ebeveynin girdiği takma addır — çocuğun
/// gerçek adını istemiyoruz, ayarlar ekranında da bu böyle anlatılıyor.
/// </remarks>
[Table("child_profiles")]
public sealed class ChildProfileRow
{
    [PrimaryKey, AutoIncrement]
    [Column("id")]
    public int Id { get; set; }

    [Column("display_name"), MaxLength(24), NotNull]
    public string DisplayName { get; set; } = string.Empty;

    /// <summary><c>AgeBandExtensions.ToId()</c> değeri.</summary>
    [Column("age_band_id"), MaxLength(16), NotNull]
    public string AgeBandId { get; set; } = "fidan";

    /// <summary>Avatar görselinin sabit anahtarı.</summary>
    [Column("avatar_id"), MaxLength(32), NotNull]
    public string AvatarId { get; set; } = string.Empty;

    /// <summary>
    /// Seçili görsel tema (orman, uzay, deniz...). Aynı oyunlar farklı temayla
    /// paketlendiği için profil başına tutuluyor.
    /// </summary>
    [Column("theme_id"), MaxLength(24), NotNull]
    public string ThemeId { get; set; } = "forest";

    [Column("created_at")]
    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Bir profilin tek bir mini oyundaki ilerlemesi.
/// </summary>
/// <remarks>
/// Bant da anahtarın parçası: çocuk büyüyüp Fidan'dan Meşe'ye geçtiğinde eski
/// yıldızları silinmez, yeni bantta sıfırdan toplamaya başlar.
/// sqlite-net bileşik birincil anahtarı desteklemediği için üçlü,
/// <see cref="MakeKey"/> ile tek bir metin anahtara katlanıyor.
/// </remarks>
[Table("game_progress")]
public sealed class GameProgressRow
{
    [PrimaryKey]
    [Column("key")]
    public string Key { get; set; } = string.Empty;

    [Indexed(Name = "ix_progress_profile", Order = 1)]
    [Column("profile_id")]
    public int ProfileId { get; set; }

    [Column("game_id"), MaxLength(40), NotNull]
    public string GameId { get; set; } = string.Empty;

    [Column("age_band_id"), MaxLength(16), NotNull]
    public string AgeBandId { get; set; } = string.Empty;

    /// <summary>O bantta bu oyundan alınmış en iyi yıldız (0-3).</summary>
    [Column("best_stars")]
    public int BestStars { get; set; }

    [Column("best_score")]
    public int BestScore { get; set; }

    [Column("play_count")]
    public int PlayCount { get; set; }

    [Column("last_played_at")]
    public DateTime? LastPlayedAtUtc { get; set; }

    public static string MakeKey(int profileId, string gameId, string ageBandId) =>
        $"{profileId}:{gameId}:{ageBandId}";
}

/// <summary>
/// Kazanılmış rozet.
/// </summary>
/// <remarks>
/// Yıldızlar sayısal ilerleme, rozetler ise koleksiyon ekranını dolduran
/// görünür ödüller — küçük yaşta asıl motivasyon bunlar.
/// </remarks>
[Table("badge_unlocks")]
public sealed class BadgeUnlockRow
{
    [PrimaryKey]
    [Column("key")]
    public string Key { get; set; } = string.Empty;

    [Indexed(Name = "ix_badge_profile", Order = 1)]
    [Column("profile_id")]
    public int ProfileId { get; set; }

    [Column("badge_id"), MaxLength(40), NotNull]
    public string BadgeId { get; set; } = string.Empty;

    [Column("unlocked_at")]
    public DateTime UnlockedAtUtc { get; set; } = DateTime.UtcNow;

    public static string MakeKey(int profileId, string badgeId) => $"{profileId}:{badgeId}";
}

/// <summary>
/// Uygulama geneli ayar (seçili profil, dil, ses, abonelik önbelleği).
/// </summary>
/// <remarks>
/// Anahtar/değer olarak duruyor, çünkü bu tablo şema değişikliği olmadan
/// büyüyecek; her yeni ayar için migration yazmak istemiyoruz.
/// </remarks>
[Table("app_settings")]
public sealed class AppSettingRow
{
    [PrimaryKey]
    [Column("key"), MaxLength(48)]
    public string Key { get; set; } = string.Empty;

    [Column("value"), NotNull]
    public string Value { get; set; } = string.Empty;
}
