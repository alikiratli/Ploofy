using Ploofy.Engine;
using Ploofy.Engine.Catalog;

namespace Ploofy.Engine.Tests;

public class GameCatalogTests
{
    [Fact]
    public void Ids_are_unique_because_saved_stars_are_keyed_by_them()
    {
        var ids = GameCatalog.Games.Select(g => g.Id).ToList();
        Assert.Equal(ids.Count, ids.Distinct().Count());
    }

    [Fact]
    public void The_library_is_seven_fun_games_and_five_learning_games()
    {
        // 1.0 yedi + üç ile çıktı; Harf Yazma ve Örüntü sürümden sonra
        // eklendi ve öğretici tarafı beşe çıkardı. Sayı burada duruyor ki
        // kütüphaneye bir oyun sessizce girmesin: yeni oyun bilinçli bir karar.
        Assert.Equal(12, GameCatalog.Games.Count);
        Assert.Equal(5, GameCatalog.Educational.Count);
        Assert.Equal(7, GameCatalog.Games.Count(g => !g.IsEducational));
    }

    [Fact]
    public void The_library_covers_five_different_interaction_kinds()
    {
        // "Hepsi aynı hissettiriyor" sorununu baştan çözen tek ölçüt bu.
        var kinds = GameCatalog.Games.Select(g => g.Interaction).Distinct();
        Assert.Equal(Enum.GetValues<InteractionKind>().Length, kinds.Count());
    }

    [Fact]
    public void Both_free_games_are_playable_by_the_youngest_band()
    {
        // İlk açılışta 2 yaşındaki çocuk da hemen bir şey oynayabilmeli.
        Assert.Equal(2, GameCatalog.Free.Count);
        Assert.All(GameCatalog.Free, g => Assert.Equal(AgeBand.Filiz, g.MinBand));
    }

    [Fact]
    public void Learning_games_start_at_the_band_where_letters_and_numbers_mean_something()
    {
        // Kural harfe ve sayıya bakıyor, "öğretici" etiketine değil: iki
        // yaşındaki çocuğa A'yı göstermenin karşılığı yok.
        var withSymbols = GameCatalog.Educational.Where(g => g.Id != GameCatalog.Pattern);
        Assert.All(withSymbols, g => Assert.True(g.MinBand >= AgeBand.Fidan));
    }

    [Fact]
    public void The_one_learning_game_without_letters_or_numbers_starts_earlier()
    {
        // Örüntü ne harf ne sayı kullanıyor: Filiz bandında yalnızca AB
        // dizisi ve renk değişimi var, ki oradaki amaç örüntü kurmak değil
        // "bir şey tekrar ediyor" fikrini yakalamak. Sayılardan önce gelen
        // beceri bu.
        var pattern = GameCatalog.ById(GameCatalog.Pattern);

        Assert.True(pattern.IsEducational);
        Assert.Equal(AgeBand.Filiz, pattern.MinBand);
    }

    [Fact]
    public void Filtering_by_band_hides_games_that_are_too_old_for_the_child()
    {
        var filiz = GameCatalog.ForBand(AgeBand.Filiz);

        Assert.DoesNotContain(filiz, g => g.Id == GameCatalog.LetterHunt);
        Assert.Contains(filiz, g => g.Id == GameCatalog.MemoryMatch);

        // Meşe her şeyi görür — kilitliler dahil, abonelik neyi açtığını anlatsın.
        Assert.Equal(GameCatalog.Games.Count, GameCatalog.ForBand(AgeBand.Mese).Count);
    }

    [Fact]
    public void Lookup_by_id_fails_loudly_for_an_unknown_game()
    {
        Assert.Equal(GameCatalog.MemoryMatch, GameCatalog.ById(GameCatalog.MemoryMatch).Id);
        Assert.Null(GameCatalog.TryById("olmayan_oyun"));
        Assert.Throws<ArgumentException>(() => GameCatalog.ById("olmayan_oyun"));
    }
}
