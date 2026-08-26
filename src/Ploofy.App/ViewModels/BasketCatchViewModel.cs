using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Engine.Catalog;
using Ploofy.Engine.Difficulty;
using Ploofy.Engine.Games;
using Ploofy.Engine.Progress;
using Ploofy.Engine.Sessions;
using Ploofy.Ui.Feedback;

namespace Ploofy.App.ViewModels;

/// <summary>
/// Sepeti Tut'un ekranı.
/// </summary>
/// <remarks>
/// Kurallar <see cref="BasketCatchRound"/>, çizim ve sürükleme
/// <c>BasketCatchSurface</c> içinde. Balon Patlatma ile aynı kalıp: yüzey
/// turu her karede kendisi ilerletiyor, burada kalan iş üstteki bilgi şeridi
/// ve turun bitişini ilerleme kaydına bağlamak.
/// </remarks>
public sealed partial class BasketCatchViewModel : ObservableObject, IDisposable
{
    private readonly ProgressRepository _repository;
    private readonly PlayFlow _flow;
    private readonly IFeedbackService _feedback;

    private TurnController? _controller;
    private BasketCatchRound? _round;
    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];

    public BasketCatchViewModel(
        ProgressRepository repository,
        PlayFlow flow,
        IFeedbackService feedback)
    {
        _repository = repository;
        _flow = flow;
        _feedback = feedback;

        PlayerName = string.Empty;
        PlayerAvatar = string.Empty;
        HandoffText = string.Empty;
    }

    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [ObservableProperty]
    public partial string PlayerAvatar { get; set; }

    [ObservableProperty]
    public partial bool ShowsProgressCounter { get; set; }

    [ObservableProperty]
    public partial int Caught { get; set; }

    [ObservableProperty]
    public partial int Goal { get; set; }

    /// <summary>Kaçırma sayacı — yalnızca kaçırmanın hata sayıldığı bantta.</summary>
    [ObservableProperty]
    public partial bool ShowsMissed { get; set; }

    [ObservableProperty]
    public partial int Missed { get; set; }

    [ObservableProperty]
    public partial bool ShowsHandoff { get; set; }

    [ObservableProperty]
    public partial string HandoffText { get; set; }

    public ObservableCollection<ProgressPip> Pips { get; } = [];

    /// <summary>Yeni tur hazır — sayfa çizim yüzeyini bununla başlatıyor.</summary>
    public event EventHandler<BasketCatchRound>? RoundReady;

    public async Task LoadAsync()
    {
        var session = _flow.PendingSession;
        if (session is null || session.GameId != GameCatalog.BasketCatch)
        {
            await Shell.Current.GoToAsync("..");
            return;
        }

        _results.Clear();

        _controller = new TurnController(session);
        _controller.StateChanged += OnTurnStateChanged;

        await _controller.StartAsync();
        ApplyState(_controller.State);
    }

    private void OnTurnStateChanged(object? sender, TurnState state) => ApplyState(state);

    private void ApplyState(TurnState state)
    {
        switch (state.Phase)
        {
            case TurnPhase.Handoff:
                ShowsHandoff = true;
                HandoffText = LocalizationService.Instance.Format(
                    "HandoffTitle", state.CurrentPlayer!.DisplayName);
                _ = _feedback.PlayAsync(FeedbackCue.Handoff);
                break;

            case TurnPhase.Playing:
                ShowsHandoff = false;
                StartRoundFor(state.CurrentPlayer!);
                break;
        }
    }

    private void StartRoundFor(Player player)
    {
        PlayerName = player.DisplayName;
        PlayerAvatar = player.AvatarId;

        var round = BasketCatchRound.ForBand(player.Band);
        _round = round;

        Goal = round.Goal;
        Caught = 0;
        Missed = 0;
        ShowsProgressCounter = DifficultyProfile.For(player.Band).UsesWrittenText;

        // Kaçırma sayısını göstermek ancak sayıldığı yerde dürüst. Sayılmadığı
        // bantta göstermek, ceza olmayan bir şeyi ceza gibi gösteriyor.
        ShowsMissed = round.CountsMisses && ShowsProgressCounter;

        Pips.Clear();
        for (var i = 0; i < round.Goal; i++)
        {
            Pips.Add(new ProgressPip());
        }

        _clock.Restart();
        RoundReady?.Invoke(this, round);
    }

    /// <summary>Çizim yüzeyinden gelen yakalama ya da kaçırma.</summary>
    public void OnCatch(bool caught)
    {
        if (_round is null)
        {
            return;
        }

        if (caught)
        {
            _ = _feedback.PlayAsync(FeedbackCue.Correct);
        }
        else if (_round.CountsMisses)
        {
            // Kaçırmanın sesi yalnızca sayıldığı bantta. Altındaki bantta
            // kaçan nesne sessizce yere düşüyor ve bir sonraki geliyor.
            _ = _feedback.PlayAsync(FeedbackCue.Retry);
        }

        Caught = _round.Caught;
        Missed = _round.Missed;

        for (var i = 0; i < Pips.Count; i++)
        {
            Pips[i].IsFilled = i < _round.Caught;
        }
    }

    [RelayCommand]
    private async Task ConfirmHandoffAsync()
    {
        if (_controller is not null)
        {
            await _feedback.PlayAsync(FeedbackCue.Tap);
            await _controller.ConfirmHandoffAsync();
        }
    }

    /// <summary>Hedefe ulaşıldı.</summary>
    public async Task CompleteRoundAsync()
    {
        if (_round is not { } round || _controller is null)
        {
            return;
        }

        _clock.Stop();
        await _feedback.PlayAsync(FeedbackCue.RoundComplete);

        var player = _controller.State.CurrentPlayer!;
        var outcome = new RoundOutcome(
            GameCatalog.BasketCatch,
            player.ProfileId,
            player.Band,
            // Bu oyunda kaybetme yok: tur ancak hedef sayıda nesne
            // yakalanınca bitiyor, kaçırmak yalnızca yıldızı etkiliyor.
            Completed: true,
            Correct: round.Caught,
            Mistakes: round.Mistakes,
            Elapsed: _clock.Elapsed,
            // Hedef süre yok: turun temposunu çocuk değil nesnelerin doğma
            // aralığı belirliyor. Bkz. BasketCatchTuning.
            ParTime: null);

        var stars = await _repository.RecordRoundAsync(outcome);
        var score = StarRating.RawScore(outcome);

        _results.Add(new PlayerResult(player.DisplayName, player.AvatarId, stars, score));

        if (stars > 0)
        {
            await _feedback.PlayAsync(FeedbackCue.StarEarned);
        }

        await _controller.FinishTurnAsync(score);

        if (_controller.State.Phase == TurnPhase.Finished)
        {
            _flow.LastSummary = new RoundSummary(
                GameCatalog.BasketCatch,
                _results.ToList(),
                _controller.Session.IsMultiplayer);

            await Shell.Current.GoToAsync("result");
        }
    }

    [RelayCommand]
    private static async Task QuitAsync() => await Shell.Current.GoToAsync("..");

    public void Dispose()
    {
        if (_controller is not null)
        {
            _controller.StateChanged -= OnTurnStateChanged;
            _ = _controller.DisposeAsync();
            _controller = null;
        }
    }
}
