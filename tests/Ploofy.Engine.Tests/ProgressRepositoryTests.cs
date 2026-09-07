using Ploofy.Data;
using Ploofy.Engine;
using Ploofy.Engine.Catalog;
using Ploofy.Engine.Difficulty;
using Ploofy.Engine.Progress;

namespace Ploofy.Engine.Tests;

/// <summary>
/// Gerçek SQLite dosyasına karşı çalışır — sahte depo değil.
/// </summary>
/// <remarks>
/// İlerleme kaydının bozulması çocuğun yıldızlarının kaybolması demek; bu
/// yüzden asıl motorun asıl veritabanıyla test edilmesi gerekiyor.
/// </remarks>
public sealed class ProgressRepositoryTests : IAsyncLifetime
{
    private readonly string _path = Path.Combine(
        Path.GetTempPath(), $"ploofy_test_{Guid.NewGuid():N}.db3");

    private ProgressDatabase _database = null!;
    private ProgressRepository _repository = null!;

    public async Task InitializeAsync()
    {
        _database = new ProgressDatabase(_path);
        await _database.InitializeAsync();
        _repository = new ProgressRepository(_database);
    }

    public async Task DisposeAsync()
    {
        await _database.DisposeAsync();
        if (File.Exists(_path))
        {
            File.Delete(_path);
        }
    }

    private static RoundOutcome Outcome(
        int profileId,
        AgeBand band,
        int correct = 6,
        int mistakes = 0,
        bool completed = true) =>
        new(
            GameCatalog.MemoryMatch,
            profileId,
            band,
            completed,
            correct,
            mistakes,
            TimeSpan.FromSeconds(40));

    // --- Oyun süresi sınırı ---

    [Fact]
    public async Task A_new_profile_has_no_play_time_limit()
    {
        // Varsayılanın kapalı olması şart: açık gelseydi güncellemeden sonra
        // bütün çocuklar birden kilitlenir ve kimse sebebini bilmezdi.
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");

        Assert.Equal(ScreenTimeBudget.Unlimited, await _repository.ScreenTimeLimitAsync(profile.Id));
        Assert.True((await _repository.ScreenTimeTodayAsync(profile.Id)).IsUnlimited);
    }

    [Fact]
    public async Task A_play_time_limit_survives_a_round_trip()
    {
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");

        await _repository.SetScreenTimeLimitAsync(profile.Id, 20);

        Assert.Equal(20, await _repository.ScreenTimeLimitAsync(profile.Id));

        // Sıfır sınırı kaldırıyor — ayrı bir bayrak yok.
        await _repository.SetScreenTimeLimitAsync(profile.Id, ScreenTimeBudget.Unlimited);

        Assert.Equal(ScreenTimeBudget.Unlimited, await _repository.ScreenTimeLimitAsync(profile.Id));
    }

    [Fact]
    public async Task Todays_rounds_count_against_the_limit()
    {
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");
        await _repository.SetScreenTimeLimitAsync(profile.Id, 10);

        await _repository.RecordRoundAsync(Outcome(profile.Id, AgeBand.Fidan));

        var status = await _repository.ScreenTimeTodayAsync(profile.Id);

        Assert.False(status.IsUnlimited);
        Assert.Equal(TimeSpan.FromSeconds(40), status.Used);
        Assert.False(status.IsSpent);
    }

    [Fact]
    public async Task Each_child_has_their_own_budget()
    {
        // Kardeşlerin bütçeleri ayrı: biri süresini bitirince öteki
        // etkilenmiyor.
        var ada = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");
        var efe = await _repository.CreateProfileAsync("Efe", AgeBand.Fidan, "bear");

        await _repository.SetScreenTimeLimitAsync(ada.Id, 10);

        Assert.Equal(10, await _repository.ScreenTimeLimitAsync(ada.Id));
        Assert.Equal(ScreenTimeBudget.Unlimited, await _repository.ScreenTimeLimitAsync(efe.Id));
    }

    [Fact]
    public async Task Deleting_a_profile_clears_its_limit()
    {
        // sqlite-net id'leri yeniden kullanabiliyor; kalan bir ayar, aynı
        // numarayı alan yeni çocuğa devredilmiş bir sınır demek olurdu.
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");
        await _repository.SetScreenTimeLimitAsync(profile.Id, 20);

        await _repository.DeleteProfileAsync(profile.Id);

        Assert.Equal(
            ScreenTimeBudget.Unlimited,
            await _repository.ScreenTimeLimitAsync(profile.Id));
    }

    [Fact]
    public async Task A_created_profile_comes_back_with_its_band()
    {
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Filiz, "fox");

        var loaded = await _repository.ProfileByIdAsync(profile.Id);

        Assert.NotNull(loaded);
        Assert.Equal("Ada", loaded.DisplayName);
        Assert.Equal(AgeBand.Filiz, AgeBandExtensions.FromId(loaded.AgeBandId));
        Assert.Equal(AgeBand.Filiz, ProgressRepository.ToPlayer(loaded).Band);
    }

    [Fact]
    public async Task Recording_a_round_returns_the_stars_it_earned()
    {
        var profile = await _repository.CreateProfileAsync("Efe", AgeBand.Mese, "bear");

        var stars = await _repository.RecordRoundAsync(
            Outcome(profile.Id, AgeBand.Mese, correct: 10, mistakes: 0));

        Assert.Equal(3, stars);
        Assert.Equal(3, await _repository.TotalStarsAsync(profile.Id));
    }

    [Fact]
    public async Task A_worse_round_never_takes_away_what_the_child_already_earned()
    {
        var profile = await _repository.CreateProfileAsync("Efe", AgeBand.Mese, "bear");

        await _repository.RecordRoundAsync(Outcome(profile.Id, AgeBand.Mese, correct: 10));
        await _repository.RecordRoundAsync(
            Outcome(profile.Id, AgeBand.Mese, correct: 2, mistakes: 8));

        var row = Assert.Single(await _repository.ProgressForAsync(profile.Id));

        Assert.Equal(3, row.BestStars);
        Assert.Equal(100, row.BestScore);
        Assert.Equal(2, row.PlayCount);
    }

    [Fact]
    public async Task Growing_into_a_new_band_starts_a_fresh_row_without_losing_the_old_one()
    {
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");

        await _repository.RecordRoundAsync(Outcome(profile.Id, AgeBand.Fidan));
        await _repository.RecordRoundAsync(Outcome(profile.Id, AgeBand.Mese));

        var rows = await _repository.ProgressForAsync(profile.Id);

        Assert.Equal(2, rows.Count);
        Assert.Equal(6, await _repository.TotalStarsAsync(profile.Id));
    }

    [Fact]
    public async Task Progress_is_kept_apart_per_child()
    {
        var ada = await _repository.CreateProfileAsync("Ada", AgeBand.Filiz, "fox");
        var efe = await _repository.CreateProfileAsync("Efe", AgeBand.Mese, "bear");

        await _repository.RecordRoundAsync(Outcome(ada.Id, AgeBand.Filiz));

        Assert.Equal(3, await _repository.TotalStarsAsync(ada.Id));
        Assert.Equal(0, await _repository.TotalStarsAsync(efe.Id));
    }

    [Fact]
    public async Task Deleting_a_profile_takes_its_progress_and_badges_with_it()
    {
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Filiz, "fox");
        await _repository.RecordRoundAsync(Outcome(profile.Id, AgeBand.Filiz));
        await _repository.UnlockBadgeAsync(profile.Id, "first_star");

        await _repository.DeleteProfileAsync(profile.Id);

        Assert.Null(await _repository.ProfileByIdAsync(profile.Id));
        Assert.Empty(await _repository.ProgressForAsync(profile.Id));
        Assert.Empty(await _repository.BadgesForAsync(profile.Id));
    }

    [Fact]
    public async Task Unlocking_the_same_badge_twice_does_not_duplicate_it()
    {
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Filiz, "fox");

        await _repository.UnlockBadgeAsync(profile.Id, "first_star");
        await _repository.UnlockBadgeAsync(profile.Id, "first_star");

        Assert.Single(await _repository.BadgesForAsync(profile.Id));
    }

    // --- Bant içi uyarlama ---

    /// <summary>Aynı oyundan arka arkaya kusursuz turlar kaydeder.</summary>
    private async Task PerfectRoundsAsync(int profileId, AgeBand band, int count)
    {
        for (var i = 0; i < count; i++)
        {
            await _repository.RecordRoundAsync(Outcome(profileId, band, mistakes: 0));
        }
    }

    [Fact]
    public async Task Adaptation_is_on_by_default()
    {
        // Sınırın tersine: uyarlama kimseyi kilitlemiyor, yalnızca zaten
        // ustalaşılmış bir oyunu zorlaştırıyor ve her yerde görünür duruyor.
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");

        Assert.True(await _repository.AdaptiveDifficultyEnabledAsync(profile.Id));
    }

    [Fact]
    public async Task Three_perfect_rounds_move_that_one_game_up()
    {
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");

        await PerfectRoundsAsync(profile.Id, AgeBand.Fidan, 2);
        Assert.Equal(
            DifficultyStep.Base,
            await _repository.StepForAsync(profile.Id, GameCatalog.MemoryMatch, AgeBand.Fidan));

        await PerfectRoundsAsync(profile.Id, AgeBand.Fidan, 1);
        Assert.Equal(
            DifficultyStep.Stretch,
            await _repository.StepForAsync(profile.Id, GameCatalog.MemoryMatch, AgeBand.Fidan));

        // Yalnızca o oyun: yapbozda ustalaşmak Yolu Bul'u zorlaştırmıyor.
        Assert.Equal(
            DifficultyStep.Base,
            await _repository.StepForAsync(profile.Id, GameCatalog.Jigsaw, AgeBand.Fidan));
    }

    [Fact]
    public async Task A_round_with_a_mistake_brings_the_game_back_down()
    {
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");

        await PerfectRoundsAsync(profile.Id, AgeBand.Fidan, 3);
        await _repository.RecordRoundAsync(Outcome(profile.Id, AgeBand.Fidan, mistakes: 4));

        Assert.Equal(
            DifficultyStep.Base,
            await _repository.StepForAsync(profile.Id, GameCatalog.MemoryMatch, AgeBand.Fidan));
    }

    [Fact]
    public async Task The_parent_can_switch_adaptation_off()
    {
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");
        await PerfectRoundsAsync(profile.Id, AgeBand.Fidan, 3);

        await _repository.SetAdaptiveDifficultyAsync(profile.Id, false);

        Assert.Equal(
            DifficultyStep.Base,
            await _repository.StepForAsync(profile.Id, GameCatalog.MemoryMatch, AgeBand.Fidan));
        Assert.Empty(await _repository.StepsForAsync(profile.Id, AgeBand.Fidan));
    }

    [Fact]
    public async Task Mastery_in_one_band_does_not_carry_into_the_next()
    {
        // Bant zaten bir kademe yukarısı; eski bandın ustalığı yeni bantta
        // ikinci bir kademe olurdu.
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Filiz, "fox");
        await PerfectRoundsAsync(profile.Id, AgeBand.Filiz, 3);

        Assert.Equal(
            DifficultyStep.Stretch,
            await _repository.StepForAsync(profile.Id, GameCatalog.MemoryMatch, AgeBand.Filiz));
        Assert.Equal(
            DifficultyStep.Base,
            await _repository.StepForAsync(profile.Id, GameCatalog.MemoryMatch, AgeBand.Fidan));
    }

    [Fact]
    public async Task The_step_rides_along_with_the_player()
    {
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");
        await PerfectRoundsAsync(profile.Id, AgeBand.Fidan, 3);

        var player = await _repository.ToPlayerAsync(profile, GameCatalog.MemoryMatch);

        Assert.Equal(DifficultyStep.Stretch, player.Step);
        Assert.Equal(AgeBand.Mese, player.KnobBand);
        Assert.Equal(AgeBand.Fidan, player.Band);
    }

    [Fact]
    public async Task Only_mastered_games_are_listed()
    {
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");
        await PerfectRoundsAsync(profile.Id, AgeBand.Fidan, 3);

        var steps = await _repository.StepsForAsync(profile.Id, AgeBand.Fidan);

        Assert.Equal([GameCatalog.MemoryMatch], steps.Keys);

        // Meşe'nin üstünde gidilecek bir bant yok.
        Assert.Empty(await _repository.StepsForAsync(profile.Id, AgeBand.Mese));
    }

    [Fact]
    public async Task Deleting_a_profile_takes_its_adaptation_setting_with_it()
    {
        // Silinmese, aynı id'yi alan sonraki profil kapalı başlardı.
        var profile = await _repository.CreateProfileAsync("Ada", AgeBand.Fidan, "fox");
        await _repository.SetAdaptiveDifficultyAsync(profile.Id, false);

        await _repository.DeleteProfileAsync(profile.Id);

        Assert.True(await _repository.AdaptiveDifficultyEnabledAsync(profile.Id));
    }

    [Fact]
    public async Task Settings_round_trip_and_fall_back_when_unset()
    {
        Assert.Null(await _repository.GetSettingAsync(SettingKeys.Locale));
        Assert.True(await _repository.GetBoolSettingAsync(SettingKeys.SoundEnabled, orElse: true));

        await _repository.SetSettingAsync(SettingKeys.Locale, "de");
        await _repository.SetBoolSettingAsync(SettingKeys.SoundEnabled, false);

        Assert.Equal("de", await _repository.GetSettingAsync(SettingKeys.Locale));
        Assert.False(await _repository.GetBoolSettingAsync(SettingKeys.SoundEnabled, orElse: true));
    }
}
