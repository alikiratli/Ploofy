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
/// Sırala'nın ekranı.
/// </summary>
/// <remarks>
/// Kurallar <see cref="LineUpRound"/>, çizim ve sürükleme
/// <c>LineUpSurface</c> içinde. Burada kalan iş üstteki bilgi şeridi —
/// hangi yönde sıralanacağı dahil — ve turun bitişini ilerleme kaydına
/// bağlamak.
/// </remarks>
public sealed partial class LineUpViewModel : ObservableObject, IDisposable
{
    private readonly ProgressRepository _repository;
    private readonly PlayFlow _flow;
    private readonly IFeedbackService _feedback;

    private TurnController? _controller;
    private LineUpRound? _round;
    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];

    public LineUpViewModel(
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
    }

    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [ObservableProperty]
    public partial string PlayerAvatar { get; set; }

    /// <summary>
    /// Yön yazısı — "önce en küçük" gibi.
    /// </summary>
    /// <remarks>
    /// Yalnızca okuyan bantlarda gösteriliyor. Okumayan çocuk yönü, yuvaların
    /// üstündeki küçük ve büyük daireden anlıyor; yazı ebeveyne ve büyük
    /// çocuğa ek bir teyit.
    /// </remarks>
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
    public event EventHandler<LineUpRound>? RoundReady;

    public async Task LoadAsync()
    {
        var session = _flow.PendingSession;
        if (session is null || session.GameId != GameCatalog.LineUp)
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

        var round = LineUpRound.ForBand(player.Band);
        _round = round;

        Total = round.Total;
        Completed = 0;
        ShowsProgressCounter = DifficultyProfile.For(player.Band).UsesWrittenText;

        Pips.Clear();
        for (var i = 0; i < round.Total; i++)
        {
            Pips.Add(new ProgressPip());
        }

        ShowHint();

        _clock.Restart();
        RoundReady?.Invoke(this, round);
    }

    private void ShowHint()
    {
        if (_round is not { } round || !ShowsProgressCounter)
        {
            Hint = string.Empty;
            return;
        }

        var ascending = round.Direction == SortDirection.Ascending;

        Hint = LocalizationService.Instance[round.Attribute switch
        {
            SortAttribute.Size => ascending ? "LineUpHintSize" : "LineUpHintSizeDown",
            _ => ascending ? "LineUpHintCount" : "LineUpHintCountDown",
        }];
    }

    /// <summary>Çizim yüzeyinden gelen bırakma olayı.</summary>
    public void OnPlaced(PlaceOutcome outcome)
    {
        if (_round is not { } round)
        {
            return;
        }

        switch (outcome)
        {
            case PlaceOutcome.WrongSlot:
                _ = _feedback.PlayAsync(FeedbackCue.Retry);
                break;

            case PlaceOutcome.Fitted:
                _ = _feedback.PlayAsync(FeedbackCue.Correct);

                Completed = round.Completed;
                for (var i = 0; i < Pips.Count; i++)
                {
                    Pips[i].IsFilled = i < round.Completed;
                }

                break;
        }
    }

    /// <summary>
    /// Bir bulmaca çözüldü.
    /// </summary>
    /// <remarks>
    /// Yüzey tamamlanmış diziyi kısa süre gösterip sıradakini istiyor; yön
    /// o anda değişebildiği için yazı da o anda tazeleniyor.
    /// </remarks>
    public void OnPuzzleSolved() => ShowHint();

    [RelayCommand]
    private async Task ConfirmHandoffAsync()
    {
        if (_controller is not null)
        {
            await _feedback.PlayAsync(FeedbackCue.Tap);
            await _controller.ConfirmHandoffAsync();
        }
    }

    /// <summary>Bütün bulmacalar çözüldü.</summary>
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
            GameCatalog.LineUp,
            player.ProfileId,
            player.Band,
            // Kaybetme yok: yanlış yuva bulmacayı bitirmiyor, yalnızca yıldızı
            // etkiliyor.
            Completed: true,
            Correct: round.Completed,
            Mistakes: round.Mistakes,
            Elapsed: _clock.Elapsed,
            ParTime: round.ParTime);

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
                GameCatalog.LineUp,
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
