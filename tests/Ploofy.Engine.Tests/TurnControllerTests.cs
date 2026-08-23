using Ploofy.Engine;
using Ploofy.Engine.Sessions;

namespace Ploofy.Engine.Tests;

public class TurnControllerTests
{
    private static Player Child(int id, string name, AgeBand band) =>
        new(id, name, band, AvatarId: "fox");

    private static readonly Player Ada = Child(1, "Ada", AgeBand.Filiz);
    private static readonly Player Efe = Child(2, "Efe", AgeBand.Mese);

    [Fact]
    public async Task Solo_play_skips_the_handoff_screen()
    {
        await using var controller = new TurnController(
            GameSession.Solo(GameCatalogIds.MemoryMatch, Ada));

        await controller.StartAsync();

        Assert.Equal(TurnPhase.Playing, controller.State.Phase);
        Assert.Equal(Ada, controller.State.CurrentPlayer);
    }

    [Fact]
    public async Task Solo_play_finishes_after_a_single_turn()
    {
        await using var controller = new TurnController(
            GameSession.Solo(GameCatalogIds.MemoryMatch, Ada));

        await controller.StartAsync();
        await controller.FinishTurnAsync(score: 40);

        Assert.Equal(TurnPhase.Finished, controller.State.Phase);
        Assert.Null(controller.State.CurrentPlayer);
        Assert.Equal(40, controller.State.Scores[Ada.ProfileId]);
    }

    [Fact]
    public async Task Pass_and_play_stops_at_a_handoff_before_every_turn()
    {
        await using var controller = new TurnController(new GameSession(
            GameCatalogIds.MemoryMatch,
            SessionMode.PassAndPlay,
            [Ada, Efe]));

        await controller.StartAsync();

        // Devir ekranı olmadan çocuk kardeşinin turunu yanlışlıkla oynuyor.
        Assert.Equal(TurnPhase.Handoff, controller.State.Phase);
        Assert.Equal(Ada, controller.State.CurrentPlayer);

        await controller.ConfirmHandoffAsync();
        Assert.Equal(TurnPhase.Playing, controller.State.Phase);

        await controller.FinishTurnAsync(score: 10);
        Assert.Equal(TurnPhase.Handoff, controller.State.Phase);
        Assert.Equal(Efe, controller.State.CurrentPlayer);
    }

    [Fact]
    public async Task Finishing_is_ignored_while_the_handoff_is_still_pending()
    {
        await using var controller = new TurnController(new GameSession(
            GameCatalogIds.MemoryMatch,
            SessionMode.PassAndPlay,
            [Ada, Efe]));

        await controller.StartAsync();
        await controller.FinishTurnAsync(score: 99);

        Assert.Equal(TurnPhase.Handoff, controller.State.Phase);
        Assert.Equal(0, controller.State.Scores[Ada.ProfileId]);
    }

    [Fact]
    public async Task Every_player_gets_the_same_number_of_turns()
    {
        await using var controller = new TurnController(new GameSession(
            GameCatalogIds.MemoryMatch,
            SessionMode.PassAndPlay,
            [Ada, Efe],
            roundsPerPlayer: 2));

        var played = new List<int>();
        await controller.StartAsync();

        for (var i = 0; i < 4; i++)
        {
            await controller.ConfirmHandoffAsync();
            played.Add(controller.State.CurrentPlayer!.ProfileId);
            await controller.FinishTurnAsync(score: 5);
        }

        Assert.Equal(TurnPhase.Finished, controller.State.Phase);
        Assert.Equal(2, played.Count(id => id == Ada.ProfileId));
        Assert.Equal(2, played.Count(id => id == Efe.ProfileId));
        Assert.Equal(10, controller.State.Scores[Ada.ProfileId]);
        Assert.Equal(10, controller.State.Scores[Efe.ProfileId]);
    }

    [Fact]
    public async Task Standings_put_the_highest_score_first_and_leave_ties_alone()
    {
        await using var controller = new TurnController(new GameSession(
            GameCatalogIds.MemoryMatch,
            SessionMode.PassAndPlay,
            [Ada, Efe]));

        await controller.StartAsync();
        await controller.ConfirmHandoffAsync();
        await controller.FinishTurnAsync(score: 10);
        await controller.ConfirmHandoffAsync();
        await controller.FinishTurnAsync(score: 30);

        var standings = controller.State.Standings;

        Assert.Equal(Efe.ProfileId, standings[0].Key);
        Assert.Equal(30, standings[0].Value);
        Assert.Equal(10, standings[1].Value);
    }

    [Fact]
    public async Task Turn_events_travel_through_the_transport()
    {
        // Sıralı oyunda taşıma cihazın içinde; yerel ağ geldiğinde aynı olaylar
        // tel üzerinden gidecek, bu yüzden bugünden gerçek trafikle çalışıyor.
        var seen = new List<SessionEvent>();

        await using var controller = new TurnController(
            GameSession.Solo(GameCatalogIds.MemoryMatch, Ada));
        controller.EventReceived += (_, e) => seen.Add(e);

        await controller.StartAsync();
        await controller.SendMoveAsync(new Dictionary<string, object?> { ["flip"] = 3 });
        await controller.FinishTurnAsync(score: 20);

        Assert.Collection(
            seen,
            e => Assert.IsType<TurnStarted>(e),
            e => Assert.IsType<GameMove>(e),
            e => Assert.IsType<TurnFinished>(e));
    }

    [Fact]
    public async Task State_changes_are_published_to_the_ui()
    {
        var phases = new List<TurnPhase>();

        await using var controller = new TurnController(new GameSession(
            GameCatalogIds.MemoryMatch,
            SessionMode.PassAndPlay,
            [Ada, Efe]));
        controller.StateChanged += (_, s) => phases.Add(s.Phase);

        await controller.StartAsync();
        await controller.ConfirmHandoffAsync();
        await controller.FinishTurnAsync(score: 1);

        Assert.Equal(
            [TurnPhase.Handoff, TurnPhase.Playing, TurnPhase.Playing, TurnPhase.Handoff],
            phases);
    }

    [Fact]
    public void Unimplemented_modes_are_refused_rather_than_half_working()
    {
        Assert.Throws<NotSupportedException>(() => new GameSession(
            GameCatalogIds.MemoryMatch,
            SessionMode.LocalNetwork,
            [Ada, Efe]));

        Assert.Throws<NotSupportedException>(() => new GameSession(
            GameCatalogIds.MemoryMatch,
            SessionMode.FamilyLink,
            [Ada, Efe]));
    }

    [Fact]
    public void A_profile_cannot_sit_at_the_table_twice()
    {
        Assert.Throws<ArgumentException>(() => new GameSession(
            GameCatalogIds.MemoryMatch,
            SessionMode.PassAndPlay,
            [Ada, Ada]));
    }

    [Fact]
    public void Solo_mode_rejects_a_second_player()
    {
        Assert.Throws<ArgumentException>(() => new GameSession(
            GameCatalogIds.MemoryMatch,
            SessionMode.Solo,
            [Ada, Efe]));
    }
}

/// <summary>Testlerin okunurluğu için kısayol.</summary>
internal static class GameCatalogIds
{
    public const string MemoryMatch = Engine.Catalog.GameCatalog.MemoryMatch;
}
