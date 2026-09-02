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
/// Harf Yazma'nın ekranı.
/// </summary>
/// <remarks>
/// Kurallar <see cref="LetterTraceRound"/>, çizim ve parmak takibi
/// <c>LetterTraceSurface</c> içinde. Burada kalan iş üstteki bilgi şeridi —
/// hangi işaretin yazıldığı ve kaç tane kaldığı — ve turun bitişini ilerleme
/// kaydına bağlamak.
/// </remarks>
public sealed partial class LetterTraceViewModel : ObservableObject, IDisposable
{
    private readonly ProgressRepository _repository;
    private readonly PlayFlow _flow;
    private readonly IFeedbackService _feedback;

    private TurnController? _controller;
    private LetterTraceRound? _round;
    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];

    public LetterTraceViewModel(
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
        Hint = string.Empty;
        Character = string.Empty;
    }

    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [ObservableProperty]
    public partial string PlayerAvatar { get; set; }

    /// <summary>Yazılan işaretin kendisi — şeritte büyük duruyor.</summary>
    /// <remarks>
    /// Çocuk çizdiği şeklin hangi harf olduğunu ancak yanında yazılı hâlini
    /// görürse öğreniyor. Şerit yalnızca ilerleme sayacı olsaydı oyun bir
    /// çizim alıştırması olur, harf öğretmezdi.
    /// </remarks>
    [ObservableProperty]
    public partial string Character { get; set; }

    [ObservableProperty]
    public partial string Hint { get; set; }

    [ObservableProperty]
    public partial bool ShowsProgressCounter { get; set; }

    [ObservableProperty]
    public partial int Completed { get; set; }

    [ObservableProperty]
    public partial int Total { get; set; }

    [ObservableProperty]
    public partial bool ShowsHandoff { get; set; }

    [ObservableProperty]
    public partial string HandoffText { get; set; }

    public ObservableCollection<ProgressPip> Pips { get; } = [];

    /// <summary>Yeni tur hazır — sayfa çizim yüzeyini bununla başlatıyor.</summary>
    public event EventHandler<LetterTraceRound>? RoundReady;

    public async Task LoadAsync()
    {
        var session = _flow.PendingSession;
        if (session is null || session.GameId != GameCatalog.LetterTrace)
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

        var pool = LetterTraceContent.PoolFor(
            LocalizationService.Instance.CurrentLanguage, player.Band);

        var round = LetterTraceRound.ForBand(player.Band, pool);
        _round = round;

        Total = round.Total;
        Completed = 0;
        ShowsProgressCounter = DifficultyProfile.For(player.Band).UsesWrittenText;

        Pips.Clear();
        for (var i = 0; i < round.Total; i++)
        {
            Pips.Add(new ProgressPip());
        }

        ShowCurrentGlyph();

        _clock.Restart();
        RoundReady?.Invoke(this, round);
    }

    private void ShowCurrentGlyph()
    {
        if (_round is not { } round)
        {
            return;
        }

        Character = round.Current.Character;
        Hint = LocalizationService.Instance[LetterTraceContent.HintKey(round.Current)];
    }

    /// <summary>Çizim yüzeyinden gelen parmak olayı.</summary>
    public void OnTraced(TraceOutcome outcome)
    {
        if (_round is not { } round)
        {
            return;
        }

        switch (outcome)
        {
            case TraceOutcome.Started:
                _ = _feedback.PlayAsync(FeedbackCue.Tap);
                break;

            case TraceOutcome.Slipped:
                // Yalnızca çıkışın kendisinde çalıyor: motor bir çıkışı bir
                // kez bildiriyor, çizgiden uzakta gezinen parmak sesi
                // tekrarlamıyor.
                _ = _feedback.PlayAsync(FeedbackCue.Retry);
                break;

            case TraceOutcome.LevelComplete:
                _ = _feedback.PlayAsync(FeedbackCue.Correct);

                if (!round.GlyphComplete)
                {
                    // Harfin bir darbesi bitti; sayaç işaret başına ilerliyor,
                    // darbe başına değil.
                    break;
                }

                Completed = round.Completed;

                for (var i = 0; i < Pips.Count; i++)
                {
                    Pips[i].IsFilled = i < round.Completed;
                }

                if (!round.IsComplete)
                {
                    ShowCurrentGlyph();
                }

                break;
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

    /// <summary>Bütün işaretler yazıldı.</summary>
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
            GameCatalog.LetterTrace,
            player.ProfileId,
            player.Band,
            // Bu oyunda kaybetme yok: çizgiden çıkmak turu bitirmiyor,
            // yalnızca yıldızı etkiliyor.
            Completed: true,
            Correct: round.Completed,
            Mistakes: round.Mistakes,
            Elapsed: _clock.Elapsed,
            // Hedef süre yok: harfi hızlı yazmak daha iyi yazmak değil.
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
                GameCatalog.LetterTrace,
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
