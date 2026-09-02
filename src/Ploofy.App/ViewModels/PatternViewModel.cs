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
using Ploofy.Ui.Controls;
using Ploofy.Ui.Feedback;

namespace Ploofy.App.ViewModels;

/// <summary>Dizideki tek kutucuk.</summary>
public sealed partial class PatternSlot : ObservableObject
{
    [ObservableProperty]
    public partial ShapeKind Kind { get; set; }

    [ObservableProperty]
    public partial BubbleHue Hue { get; set; }

    /// <summary>Boşluk mu? Boşsa hayalet çiziliyor ve nabız gibi atıyor.</summary>
    [ObservableProperty]
    public partial bool IsEmpty { get; set; }
}

/// <summary>Alttaki seçeneklerden biri.</summary>
public sealed partial class PatternOption : ObservableObject
{
    public required int Id { get; init; }

    public required ShapeKind Kind { get; init; }

    public required BubbleHue Hue { get; init; }

    [ObservableProperty]
    public partial ShapeTileState State { get; set; }
}

/// <summary>
/// Örüntü Tamamlama'nın ekranı.
/// </summary>
/// <remarks>
/// Kurallar <see cref="PatternRound"/> içinde; burada kalan iş diziyi ve
/// seçenekleri ekrana koymak, dokunuşu motora iletmek ve turun bitişini
/// ilerleme kaydına bağlamak.
/// </remarks>
public sealed partial class PatternViewModel : ObservableObject, IDisposable
{
    private readonly ProgressRepository _repository;
    private readonly PlayFlow _flow;
    private readonly IFeedbackService _feedback;

    private TurnController? _controller;
    private PatternRound? _round;
    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];

    public PatternViewModel(
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
    public partial int Completed { get; set; }

    [ObservableProperty]
    public partial int Total { get; set; }

    [ObservableProperty]
    public partial bool ShowsHandoff { get; set; }

    [ObservableProperty]
    public partial string HandoffText { get; set; }

    public ObservableCollection<PatternSlot> Sequence { get; } = [];

    public ObservableCollection<PatternOption> Options { get; } = [];

    public ObservableCollection<ProgressPip> Pips { get; } = [];

    public async Task LoadAsync()
    {
        var session = _flow.PendingSession;
        if (session is null || session.GameId != GameCatalog.Pattern)
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

        var round = PatternRound.ForBand(player.Band);
        _round = round;

        Total = round.Total;
        Completed = 0;
        ShowsProgressCounter = DifficultyProfile.For(player.Band).UsesWrittenText;

        Pips.Clear();
        for (var i = 0; i < round.Total; i++)
        {
            Pips.Add(new ProgressPip());
        }

        ShowQuestion();

        _clock.Restart();
    }

    /// <summary>
    /// Soruyu ekrana koyar.
    /// </summary>
    /// <remarks>
    /// Koleksiyonlar temizlenip yeniden dolduruluyor. Uzunluk bant boyunca
    /// sabit olduğu için yerinde güncellemek de mümkündü, ama o zaman eski
    /// kutucuğun animasyon durumu yeni soruya taşınıyor: doğru cevabın
    /// büyümesi bir sonraki sorunun yanlış kutucuğunda görünüyordu.
    /// </remarks>
    private void ShowQuestion()
    {
        if (_round?.Current is not { } question)
        {
            return;
        }

        Sequence.Clear();
        for (var i = 0; i < question.Sequence.Count; i++)
        {
            var tile = question.Sequence[i];

            Sequence.Add(new PatternSlot
            {
                // Boşluk da bir şekil çiziyor — hayalet olarak. Hangi şekil
                // olduğu ipucu vermemeli, o yüzden dizinin ilk parçasının
                // şekli değil dairenin kendisi kullanılıyor.
                Kind = tile?.Kind ?? ShapeKind.Circle,
                Hue = tile?.Hue ?? BubbleHue.Cherry,
                IsEmpty = tile is null,
            });
        }

        Options.Clear();
        foreach (var choice in question.Choices)
        {
            Options.Add(new PatternOption
            {
                Id = choice.Id,
                Kind = choice.Tile.Kind,
                Hue = choice.Tile.Hue,
            });
        }
    }

    [RelayCommand]
    private async Task ChooseAsync(PatternOption? option)
    {
        if (option is null || _round is not { } round)
        {
            return;
        }

        var outcome = round.Tap(option.Id);

        switch (outcome)
        {
            case PatternOutcome.Wrong:
                option.State = ShapeTileState.Wrong;
                await _feedback.PlayAsync(FeedbackCue.Retry);

                // Durum geri alınıyor ki aynı kutucuğa ikinci kez basıldığında
                // silkelenme yeniden tetiklensin.
                option.State = ShapeTileState.Idle;
                break;

            case PatternOutcome.Correct:
                option.State = ShapeTileState.Correct;
                await _feedback.PlayAsync(FeedbackCue.Correct);

                Completed = round.Correct;
                for (var i = 0; i < Pips.Count; i++)
                {
                    Pips[i].IsFilled = i < round.Correct;
                }

                // Doğru parça boşluğa oturuyor ve kısa süre görünüyor:
                // tamamlanmış diziyi görmeden geçmek, çocuğun neyi doğru
                // yaptığını hiç görmemesi demek.
                FillBlank(option);
                await Task.Delay(TimeSpan.FromMilliseconds(650));

                if (round.IsComplete)
                {
                    await CompleteRoundAsync();
                    return;
                }

                ShowQuestion();
                break;
        }
    }

    private void FillBlank(PatternOption option)
    {
        foreach (var slot in Sequence)
        {
            if (!slot.IsEmpty)
            {
                continue;
            }

            slot.Kind = option.Kind;
            slot.Hue = option.Hue;
            slot.IsEmpty = false;
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
            GameCatalog.Pattern,
            player.ProfileId,
            player.Band,
            // Kaybetme yok: yanlış seçim soruyu bitirmiyor, yalnızca yıldızı
            // etkiliyor.
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
                GameCatalog.Pattern,
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
