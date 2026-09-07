using Ploofy.Engine;
using Ploofy.Engine.Catalog;
using Ploofy.Engine.Difficulty;
using Ploofy.Engine.Progress;
using Ploofy.Engine.Sessions;

namespace Ploofy.Engine.Tests;

/// <summary>
/// Bant içi uyarlama. Yanlışı ancak çocuk sebepsiz yere zorlanmaya
/// başladığında ya da hiç zorlanmadığında görülürdü; kural burada sınanıyor.
/// </summary>
public class AdaptiveDifficultyTests
{
    private static readonly DateOnly Today = new(2026, 9, 7);

    private static PlayedRound Round(
        AgeBand band, int stars = 3, int mistakes = 0, int daysAgo = 0) => new(
        Today.AddDays(-daysAgo),
        GameCatalog.MemoryMatch,
        band,
        stars,
        mistakes,
        Duration: TimeSpan.FromSeconds(50));

    private static PlayedRound[] Perfect(AgeBand band, int count) =>
        [.. Enumerable.Range(0, count).Select(_ => Round(band))];

    [Fact]
    public void A_new_game_starts_at_the_bands_own_settings()
    {
        // İlk turdan zorlaşan bir oyun kimseye bir şey anlatmaz.
        Assert.Equal(DifficultyStep.Base, AdaptiveDifficulty.Evaluate(AgeBand.Fidan, []));
    }

    [Fact]
    public void Two_perfect_rounds_are_not_enough()
    {
        // Bu yaşta iki kolay tur şansa gelebiliyor.
        Assert.Equal(
            DifficultyStep.Base,
            AdaptiveDifficulty.Evaluate(AgeBand.Fidan, Perfect(AgeBand.Fidan, 2)));
    }

    [Fact]
    public void Three_perfect_rounds_move_the_game_one_step_up()
    {
        Assert.Equal(
            DifficultyStep.Stretch,
            AdaptiveDifficulty.Evaluate(AgeBand.Fidan, Perfect(AgeBand.Fidan, 3)));
    }

    [Fact]
    public void Only_the_newest_rounds_count()
    {
        // Liste yeniden eskiye. Baştaki üç kusursuz tur yeterli; arkadaki
        // kötü turlar geçmişte kaldı.
        PlayedRound[] rounds =
        [
            .. Perfect(AgeBand.Fidan, 3),
            Round(AgeBand.Fidan, stars: 1, mistakes: 5),
        ];

        Assert.Equal(DifficultyStep.Stretch, AdaptiveDifficulty.Evaluate(AgeBand.Fidan, rounds));
    }

    [Fact]
    public void One_round_that_was_hard_brings_it_back_down()
    {
        // Kademe saklanmadığı için geri iniş kendiliğinden oluyor: zorlanan
        // çocuk için kimsenin bir şeyi sıfırlaması gerekmiyor.
        PlayedRound[] rounds =
        [
            Round(AgeBand.Fidan, stars: 2, mistakes: 2),
            .. Perfect(AgeBand.Fidan, 3),
        ];

        Assert.Equal(DifficultyStep.Base, AdaptiveDifficulty.Evaluate(AgeBand.Fidan, rounds));
    }

    [Fact]
    public void Three_stars_alone_are_not_mastery_in_the_sprout_band()
    {
        // Filiz'de bitiren herkes üç yıldız alıyor. Yıldıza bakan bir kural
        // her Filiz çocuğunu üç turda yukarı iterdi; ölçüt sıfır hata.
        var withMistakes = Enumerable.Range(0, 3)
            .Select(_ => Round(AgeBand.Filiz, stars: 3, mistakes: 1))
            .ToArray();

        Assert.Equal(DifficultyStep.Base, AdaptiveDifficulty.Evaluate(AgeBand.Filiz, withMistakes));
        Assert.Equal(
            DifficultyStep.Stretch,
            AdaptiveDifficulty.Evaluate(AgeBand.Filiz, Perfect(AgeBand.Filiz, 3)));
    }

    [Fact]
    public void Rounds_from_another_band_do_not_count()
    {
        // Bant değiştiren çocuk yeni bandına eski bandındaki ustalığıyla
        // girmemeli — yeni bant zaten bir kademe yukarısı.
        Assert.Equal(
            DifficultyStep.Base,
            AdaptiveDifficulty.Evaluate(AgeBand.Mese - 1, Perfect(AgeBand.Filiz, 5)));
    }

    [Fact]
    public void The_top_band_has_nowhere_to_go()
    {
        // Meşe'nin üstünde bir sütun yok; uydurulmuş bir sayı, olmayan bir
        // kademeden kötü olurdu.
        Assert.False(AdaptiveDifficulty.CanStretch(AgeBand.Mese));
        Assert.Equal(
            DifficultyStep.Base,
            AdaptiveDifficulty.Evaluate(AgeBand.Mese, Perfect(AgeBand.Mese, 5)));
    }

    [Fact]
    public void A_step_up_reads_the_next_bands_knobs()
    {
        Assert.Equal(AgeBand.Fidan, AdaptiveDifficulty.KnobBand(AgeBand.Filiz, DifficultyStep.Stretch));
        Assert.Equal(AgeBand.Mese, AdaptiveDifficulty.KnobBand(AgeBand.Fidan, DifficultyStep.Stretch));
        Assert.Equal(AgeBand.Mese, AdaptiveDifficulty.KnobBand(AgeBand.Mese, DifficultyStep.Stretch));
        Assert.Equal(AgeBand.Filiz, AdaptiveDifficulty.KnobBand(AgeBand.Filiz, DifficultyStep.Base));
    }

    [Fact]
    public void The_step_moves_the_knobs_but_not_the_band()
    {
        // Ayrımın kendisi: yukarı çıkan Filiz çocuğu daha çok parça görüyor
        // ama zamanlayıcı, yazı ve kaybetme yine gelmiyor — onlar zorluk
        // değil, yaşa uygunluk kuralları. Yıldız da gerçek bantla veriliyor.
        var player = new Player(1, "Ada", AgeBand.Filiz, "fox", Step: DifficultyStep.Stretch);

        Assert.Equal(AgeBand.Fidan, player.KnobBand);
        Assert.Equal(AgeBand.Filiz, player.Band);
        Assert.True(player.IsStretched);

        var profile = DifficultyProfile.For(player.Band);
        Assert.False(profile.CanFail);
        Assert.False(profile.ShowsTimer);
        Assert.False(profile.UsesWrittenText);
    }

    [Fact]
    public void A_player_starts_at_the_base_step()
    {
        // Varsayılan kademesiz: kademeyi hesaplamayı unutan bir çağrı, oyunu
        // sessizce zorlaştırmak yerine sessizce eski davranışta bırakıyor.
        var player = new Player(1, "Ada", AgeBand.Fidan, "fox");

        Assert.Equal(DifficultyStep.Base, player.Step);
        Assert.Equal(AgeBand.Fidan, player.KnobBand);
        Assert.False(player.IsStretched);
    }

    [Fact]
    public void Every_band_that_can_stretch_lands_on_a_real_band()
    {
        // Kademe hiçbir zaman tanımsız bir banda düşmemeli: BandValue.For
        // bilinmeyen bantta patlıyor.
        foreach (var band in Enum.GetValues<AgeBand>())
        {
            var knob = AdaptiveDifficulty.KnobBand(band, DifficultyStep.Stretch);

            Assert.True(Enum.IsDefined(knob));
            Assert.True(knob >= band);
            Assert.Equal(band == AgeBand.Mese, knob == band);
        }
    }
}
