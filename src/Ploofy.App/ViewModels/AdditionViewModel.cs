using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Globalization;
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

/// <summary>Seçeneklerden biri.</summary>
public sealed partial class AdditionChoice(int value, int hueIndex) : ObservableObject
{
    public int Value { get; } = value;

    public int HueIndex { get; } = hueIndex;

    public string Text { get; } = value.ToString(CultureInfo.InvariantCulture);

    [ObservableProperty]
    public partial GlyphTileStateVm State { get; set; } = GlyphTileStateVm.Idle;
}

/// <summary>
/// Basit Toplama'nın ekranı.
/// </summary>
/// <remarks>
/// Kurallar <see cref="AdditionRound"/>. Buradaki iş sorunun iki tarafını
/// göstermek: her toplanan ya bir nesne kümesi ya da bir rakam. Hangisinin
/// hangisi olduğunu bant belirliyor — Fidan iki kümeyi de sayıyor, Meşe
/// birinci sayıdan devam ediyor.
/// </remarks>
public sealed partial class AdditionViewModel : ObservableObject, IDisposable
{
    private static readonly TimeSpan FeedbackHold = TimeSpan.FromMilliseconds(450);

    private readonly ProgressRepository _repository;
    private readonly PlayFlow _flow;
    private readonly IFeedbackService _feedback;

    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];

    private TurnController? _controller;
    private AdditionRound? _round;
    private CancellationTokenSource _lifetime = new();

    public AdditionViewModel(
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
        LeftNumber = string.Empty;
    }

    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [ObservableProperty]
    public partial string PlayerAvatar { get; set; }

    /// <summary>Soldaki küme — her öğe aynı simge.</summary>
    public ObservableCollection<string> LeftObjects { get; } = [];

    /// <summary>Sağdaki küme.</summary>
    public ObservableCollection<string> RightObjects { get; } = [];

    /// <summary>Birinci toplanan rakam olarak; yalnızca Meşe'de görünüyor.</summary>
    [ObservableProperty]
    public partial string LeftNumber { get; set; }

    /// <summary>Sol taraf küme mi rakam mı?</summary>
    [ObservableProperty]
    public partial bool ShowsLeftObjects { get; set; }

    [ObservableProperty]
    public partial bool ShowsProgressCounter { get; set; }

    [ObservableProperty]
    public partial int Answered { get; set; }

    [ObservableProperty]
    public partial int Total { get; set; }

    [ObservableProperty]
    public partial bool ShowsHandoff { get; set; }

    [ObservableProperty]
    public partial string HandoffText { get; set; }

    public ObservableCollection<AdditionChoice> Choices { get; } = [];

    public ObservableCollection<ProgressPip> Pips { get; } = [];

    public async Task LoadAsync()
    {
        var session = _flow.PendingSession;
        if (session is null || session.GameId != GameCatalog.Addition)
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

        var round = AdditionRound.ForBand(player.Band);
        _round = round;

        Total = round.Total;
        Answered = 0;
        ShowsLeftObjects = round.ShowsFirstAsObjects;
        ShowsProgressCounter = DifficultyProfile.For(player.Band).UsesWrittenText;

        Pips.Clear();
        for (var i = 0; i < round.Total; i++)
        {
            Pips.Add(new ProgressPip());
        }

        ShowCurrentQuestion();
        _clock.Restart();
    }

    private void ShowCurrentQuestion()
    {
        if (_round?.Current is not { } question)
        {
            return;
        }

        LeftObjects.Clear();
        RightObjects.Clear();

        if (ShowsLeftObjects)
        {
            for (var i = 0; i < question.Left; i++)
            {
                LeftObjects.Add(question.Glyph);
            }
        }

        LeftNumber = question.Left.ToString(CultureInfo.InvariantCulture);

        for (var i = 0; i < question.Right; i++)
        {
            RightObjects.Add(question.Glyph);
        }

        Choices.Clear();
        for (var i = 0; i < question.Choices.Count; i++)
        {
            Choices.Add(new AdditionChoice(question.Choices[i], i));
        }
    }

    /// <summary>Bir seçeneğe dokunuldu.</summary>
    [RelayCommand]
    private async Task AnswerAsync(AdditionChoice? choice)
    {
        if (_round is not { } round || choice is null || round.IsComplete)
        {
            return;
        }

        var outcome = round.Answer(choice.Value);

        switch (outcome)
        {
            case AnswerOutcome.Correct:
                choice.State = GlyphTileStateVm.Correct;
                await _feedback.PlayAsync(FeedbackCue.Correct);

                Answered = round.Answered;
                for (var i = 0; i < Pips.Count; i++)
                {
                    Pips[i].IsFilled = i < round.Answered;
                }

                // Doğru cevap kısa süre yeşil kalsın, sonra sıradaki soru
                // gelsin: soru anında değişirse çocuk doğru yaptığını
                // göremiyor.
                if (await HoldAsync())
                {
                    choice.State = GlyphTileStateVm.Idle;
                    ShowCurrentQuestion();
                }

                break;

            case AnswerOutcome.Wrong:
                choice.State = GlyphTileStateVm.Wrong;
                await _feedback.PlayAsync(FeedbackCue.Retry);

                if (await HoldAsync())
                {
                    choice.State = GlyphTileStateVm.Idle;
                }

                break;

            default:
                return;
        }

        if (round.IsComplete)
        {
            await CompleteRoundAsync();
        }
    }

    /// <summary>Geri bildirim payı. Sayfa kapandıysa <c>false</c> dönüyor.</summary>
    /// <remarks>
    /// Kapanmış bir ekranda dolan bir bekleme, geri dönmüş sayfanın sorusunu
    /// değiştiriyordu — aynı tuzak Sırayı Tekrarla'da da vardı.
    /// </remarks>
    private async Task<bool> HoldAsync()
    {
        try
        {
            await Task.Delay(FeedbackHold, _lifetime.Token);
            return true;
        }
        catch (OperationCanceledException)
        {
            return false;
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
        var outcome = new RoundOutcome(
            GameCatalog.Addition,
            player.ProfileId,
            player.Band,
            // Kaybetme yok: yanlış seçenek soruyu geçirmiyor.
            Completed: true,
            Correct: round.Answered,
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
                GameCatalog.Addition,
                _results.ToList(),
                _controller.Session.IsMultiplayer);

            await Shell.Current.GoToAsync("result");
        }
    }

    [RelayCommand]
    private static async Task QuitAsync() => await Shell.Current.GoToAsync("..");

    public void Dispose()
    {
        _lifetime.Cancel();
        _lifetime.Dispose();
        _lifetime = new CancellationTokenSource();

        if (_controller is not null)
        {
            _controller.StateChanged -= OnTurnStateChanged;
            _ = _controller.DisposeAsync();
            _controller = null;
        }
    }
}
