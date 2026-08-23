using Ploofy.Data;
using Ploofy.Engine;
using Ploofy.Engine.Catalog;
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
