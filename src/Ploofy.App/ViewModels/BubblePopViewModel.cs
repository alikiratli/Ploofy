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
using Ploofy.Ui.Feedback;
using Ploofy.Ui.Painting;

namespace Ploofy.App.ViewModels;

/// <summary>
/// İlerleme göstergesindeki tek balon işareti.
/// </summary>
/// <remarks>
/// Sayı yerine işaret: "3 / 8" ifadesi Filiz ve Fidan bantlarında okunmuyor
/// ama dolan balonlar bakınca anlaşılıyor.
/// </remarks>
public sealed partial class ProgressPip : ObservableObject
{
    [ObservableProperty]
    public partial bool IsFilled { get; set; }

    public Color Fill => IsFilled ? Color.FromArgb("#FFC845") : Color.FromArgb("#E4D8C6");

    partial void OnIsFilledChanged(bool value) => OnPropertyChanged(nameof(Fill));
}

/// <summary>
/// Balon Patlatma'nın ekranı.
/// </summary>
/// <remarks>
/// <para>
/// Oyunun kuralları <see cref="BubblePopRound"/>, çizimi
/// <c>BubbleSurface</c> içinde. Burada kalan iş üstteki bilgi şeridi
/// (kim oynuyor, hedef renk, ilerleme, süre) ve turun bitişini ilerleme
/// kaydına bağlamak.
/// </para>
/// <para>
/// Bant farkı burada da görünür: Filiz'de hedef renk yok, süre yok, sayı yok —
/// yalnızca dolan balonlar. Meşe'de üçü de var.
/// </para>
/// </remarks>
public sealed partial class BubblePopViewModel : ObservableObject, IDisposable
{
    private readonly ProgressRepository _repository;
    private readonly PlayFlow _flow;
    private readonly IFeedbackService _feedback;

    public BubblePopViewModel(
        ProgressRepository repository,
        PlayFlow flow,
        IFeedbackService feedback)
    {
        _repository = repository;
        _flow = flow;
        _feedback = feedback;

        // Partial property'ler başlangıç değeri kabul etmiyor; metin alanları
        // burada boşa çekiliyor ki ekran ilk kare boş görünsün, null olmasın.
        PlayerName = string.Empty;
        PlayerAvatar = string.Empty;
        TimerText = string.Empty;
        HandoffText = string.Empty;
        TargetColor = Colors.Transparent;
    }

    private TurnController? _controller;
    private BubblePopRound? _round;
    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];

    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [ObservableProperty]
    public partial string PlayerAvatar { get; set; }

    /// <summary>Hedef rengin ekrandaki karşılığı; hedef yoksa saydam.</summary>
    [ObservableProperty]
    public partial Color TargetColor { get; set; }

    [ObservableProperty]
    public partial bool ShowsTarget { get; set; }

    [ObservableProperty]
    public partial bool ShowsTimer { get; set; }

    [ObservableProperty]
    public partial string TimerText { get; set; }

    /// <summary>Süre azaldığında sayaç kırmızıya döner.</summary>
    [ObservableProperty]
    public partial bool IsTimeRunningOut { get; set; }

    [ObservableProperty]
    public partial bool ShowsHandoff { get; set; }

    [ObservableProperty]
    public partial string HandoffText { get; set; }

    public ObservableCollection<ProgressPip> Pips { get; } = [];

    /// <summary>Yeni tur hazır — sayfa çizim yüzeyini bununla başlatıyor.</summary>
    public event EventHandler<BubblePopRound>? RoundReady;

    public async Task LoadAsync()
    {
        var session = _flow.PendingSession;
        if (session is null || session.GameId != GameCatalog.BubblePop)
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

        var round = BubblePopRound.ForBand(player.Band);
        _round = round;

        ShowsTarget = round.TargetHue is not null;
        TargetColor = round.TargetHue is { } hue
            ? ToMauiColor(PloofyPalette.For(hue).Body)
            : Colors.Transparent;

        ShowsTimer = DifficultyProfile.For(player.Band).ShowsTimer && round.TimeLimit is not null;
        IsTimeRunningOut = false;
        UpdateTimer();

        Pips.Clear();
        for (var i = 0; i < round.Goal; i++)
        {
            Pips.Add(new ProgressPip());
        }

        _clock.Restart();
        RoundReady?.Invoke(this, round);
    }

    /// <summary>Çizim yüzeyinden gelen dokunuş.</summary>
    public void OnTouched(PopOutcome outcome)
    {
        switch (outcome)
        {
            case PopOutcome.Popped:
                _ = _feedback.PlayAsync(FeedbackCue.Correct);
                SyncPips();
                break;

            case PopOutcome.WrongColor:
                _ = _feedback.PlayAsync(FeedbackCue.Retry);
                break;
        }
    }

    /// <summary>Çizim yüzeyinin her karesinde çağrılır — sayaç bunun üstünden akıyor.</summary>
    public void OnFrame()
    {
        if (ShowsTimer)
        {
            UpdateTimer();
        }
    }

    private void SyncPips()
    {
        if (_round is null)
        {
            return;
        }

        for (var i = 0; i < Pips.Count; i++)
        {
            Pips[i].IsFilled = i < _round.Popped;
        }
    }

    private void UpdateTimer()
    {
        if (_round?.Remaining is not { } remaining)
        {
            TimerText = string.Empty;
            return;
        }

        TimerText = $"{(int)remaining.TotalMinutes}:{remaining.Seconds:00}";
        IsTimeRunningOut = remaining <= TimeSpan.FromSeconds(10);
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

    /// <summary>Tur bitti: hedefe ulaşıldı ya da süre doldu.</summary>
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
            GameCatalog.BubblePop,
            player.ProfileId,
            player.Band,
            // Süre dolduysa tur tamamlanmadı; Filiz ve Fidan'da süre olmadığı
            // için bu yalnızca Meşe'de gerçekleşiyor.
            Completed: round.IsComplete,
            Correct: round.Popped,
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
                GameCatalog.BubblePop,
                _results.ToList(),
                _controller.Session.IsMultiplayer);

            await Shell.Current.GoToAsync("result");
        }
    }

    [RelayCommand]
    private static async Task QuitAsync() => await Shell.Current.GoToAsync("..");

    private static Color ToMauiColor(SkiaSharp.SKColor color) =>
        Color.FromRgba(color.Red, color.Green, color.Blue, color.Alpha);

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
