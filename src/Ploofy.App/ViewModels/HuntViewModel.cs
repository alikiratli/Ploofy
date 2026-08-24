using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Engine;
using Ploofy.Engine.Catalog;
using Ploofy.Engine.Difficulty;
using Ploofy.Engine.Games;
using Ploofy.Engine.Progress;
using Ploofy.Engine.Sessions;
using Ploofy.Ui.Controls;
using Ploofy.Ui.Feedback;

namespace Ploofy.App.ViewModels;

/// <summary>Ekrandaki tek seçenek kutucuğu.</summary>
public sealed partial class HuntChoiceVm(int id, string glyph, int hueIndex) : ObservableObject
{
    public int Id { get; } = id;

    public string Glyph { get; } = glyph;

    public int HueIndex { get; } = hueIndex;

    [ObservableProperty]
    public partial GlyphTileState State { get; set; }
}

/// <summary>
/// Harf Avı ve Sayı Avı'nın ortak ekranı.
/// </summary>
/// <remarks>
/// <para>
/// İki oyun aynı sayfayı kullanıyor; hangisi olduğu oturumdaki oyun
/// kimliğinden çözülüyor. Mekanik aynı, içerik farklı.
/// </para>
/// <para>
/// Yönerge yazıya bağlı değil: aranan işaret üstte büyük duruyor, çocuk
/// aynısını aşağıda buluyor. Böylece okuma bilmeyen Fidan bandı da
/// oynayabiliyor.
/// </para>
/// </remarks>
public sealed partial class HuntViewModel : ObservableObject, IDisposable
{
    /// <summary>Doğru/yanlış geri bildiriminin ekranda kaldığı süre.</summary>
    private static readonly TimeSpan FeedbackPause = TimeSpan.FromMilliseconds(480);

    private readonly ProgressRepository _repository;
    private readonly PlayFlow _flow;
    private readonly IFeedbackService _feedback;

    private TurnController? _controller;
    private HuntRound? _round;
    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];
    private bool _isResolving;

    public HuntViewModel(
        ProgressRepository repository,
        PlayFlow flow,
        IFeedbackService feedback)
    {
        _repository = repository;
        _flow = flow;
        _feedback = feedback;

        PlayerName = string.Empty;
        PlayerAvatar = string.Empty;
        Target = string.Empty;
        HandoffText = string.Empty;
    }

    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [ObservableProperty]
    public partial string PlayerAvatar { get; set; }

    /// <summary>Aranan harf ya da sayı.</summary>
    [ObservableProperty]
    public partial string Target { get; set; }

    [ObservableProperty]
    public partial bool ShowsProgressCounter { get; set; }

    [ObservableProperty]
    public partial int Answered { get; set; }

    [ObservableProperty]
    public partial int Total { get; set; }

    /// <summary>Seçenekler kaç sütuna dizilecek.</summary>
    [ObservableProperty]
    public partial int Columns { get; set; } = 2;

    [ObservableProperty]
    public partial bool ShowsHandoff { get; set; }

    [ObservableProperty]
    public partial string HandoffText { get; set; }

    public ObservableCollection<HuntChoiceVm> Choices { get; } = [];

    public ObservableCollection<ProgressPip> Pips { get; } = [];

    public async Task LoadAsync()
    {
        var session = _flow.PendingSession;
        if (session is null ||
            (session.GameId != GameCatalog.LetterHunt && session.GameId != GameCatalog.NumberHunt))
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
        var gameId = _flow.PendingSession!.GameId;

        PlayerName = player.DisplayName;
        PlayerAvatar = player.AvatarId;

        var pool = HuntContent.PoolFor(
            gameId,
            LocalizationService.Instance.CurrentLanguage,
            player.Band);

        var round = HuntRound.ForBand(player.Band, HuntContent.KindFor(gameId), pool);
        _round = round;

        Total = round.Total;
        Answered = 0;
        ShowsProgressCounter = DifficultyProfile.For(player.Band).UsesWrittenText;

        Pips.Clear();
        for (var i = 0; i < round.Total; i++)
        {
            Pips.Add(new ProgressPip());
        }

        _isResolving = false;
        _clock.Restart();
        ShowQuestion();
    }

    private void ShowQuestion()
    {
        if (_round?.Current is not { } question)
        {
            return;
        }

        Target = question.Target;

        Choices.Clear();
        for (var i = 0; i < question.Choices.Count; i++)
        {
            var choice = question.Choices[i];
            Choices.Add(new HuntChoiceVm(choice.Id, choice.Glyph, i));
        }

        // İki sütun ikiden fazla seçenekte dar kalıyor, üç sütun ikide boş.
        Columns = question.Choices.Count <= 2 ? 2 : 3;
    }

    [RelayCommand]
    private async Task TapAsync(HuntChoiceVm? choice)
    {
        if (choice is null || _round is null || _isResolving)
        {
            return;
        }

        var outcome = _round.Tap(choice.Id);
        if (outcome == HuntOutcome.Ignored)
        {
            return;
        }

        _isResolving = true;
        try
        {
            if (outcome == HuntOutcome.Wrong)
            {
                choice.State = GlyphTileState.Wrong;
                await _feedback.PlayAsync(FeedbackCue.Retry);
                await Task.Delay(FeedbackPause);
                choice.State = GlyphTileState.Idle;
                return;
            }

            choice.State = GlyphTileState.Correct;
            await _feedback.PlayAsync(FeedbackCue.Correct);
            SyncPips();

            await Task.Delay(FeedbackPause);

            if (_round.IsComplete)
            {
                await CompleteRoundAsync();
                return;
            }

            ShowQuestion();
        }
        finally
        {
            _isResolving = false;
        }
    }

    private void SyncPips()
    {
        if (_round is null)
        {
            return;
        }

        Answered = _round.Correct;
        for (var i = 0; i < Pips.Count; i++)
        {
            Pips[i].IsFilled = i < _round.Correct;
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

    private async Task CompleteRoundAsync()
    {
        if (_round is not { } round || _controller is null)
        {
            return;
        }

        _clock.Stop();
        await _feedback.PlayAsync(FeedbackCue.RoundComplete);

        var player = _controller.State.CurrentPlayer!;
        var gameId = _flow.PendingSession!.GameId;

        var outcome = new RoundOutcome(
            gameId,
            player.ProfileId,
            player.Band,
            // Bu oyunda kaybetme yok: tur ancak bütün sorular doğru
            // cevaplanınca bitiyor.
            Completed: true,
            Correct: round.Correct,
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
                gameId,
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
