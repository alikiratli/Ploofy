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
/// Noktaları Birleştir'in ekranı.
/// </summary>
/// <remarks>
/// Kurallar <see cref="DotToDotRound"/>, çizim ve dokunma <c>DotToDotSurface</c>
/// içinde. Burada kalan iş üstteki bilgi şeridi — hangi hayvanın çizildiği ve
/// sıradaki rakamın kaç olduğu — ve turun bitişini ilerleme kaydına bağlamak.
/// </remarks>
public sealed partial class DotToDotViewModel : ObservableObject, IDisposable
{
    private readonly ProgressRepository _repository;
    private readonly PlayFlow _flow;
    private readonly IFeedbackService _feedback;

    private TurnController? _controller;
    private DotToDotRound? _round;
    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];

    public DotToDotViewModel(
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
        PictureName = string.Empty;
        PictureGlyph = string.Empty;
        NextNumber = string.Empty;
    }

    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [ObservableProperty]
    public partial string PlayerAvatar { get; set; }

    /// <summary>Çizilen hayvanın adı — okuyan bantta şeritte duruyor.</summary>
    [ObservableProperty]
    public partial string PictureName { get; set; }

    /// <summary>
    /// Çizilen hayvanın simgesi.
    /// </summary>
    /// <remarks>
    /// Şeritte adının yerine <b>de</b> geçiyor: okuma bilmeyen çocuk neyi
    /// çizdiğini ancak simgeden anlıyor, ve "ne çizdiğini bilmek" bu oyunda
    /// sırayı takip etme isteğinin kaynağı.
    /// </remarks>
    [ObservableProperty]
    public partial string PictureGlyph { get; set; }

    /// <summary>
    /// Sıradaki rakam — şeritte büyük duruyor.
    /// </summary>
    /// <remarks>
    /// Ekranda aranan sayının bir de büyük hâlinin durması, Harf Avı'nda
    /// aranan işaretin büyük gösterilmesiyle aynı sebeple: dört yaşındaki
    /// çocuk "sıradaki kaç" sorusunu akılda tutmakla ekranda aramayı aynı
    /// anda yapamıyor.
    /// </remarks>
    [ObservableProperty]
    public partial string NextNumber { get; set; }

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
    public event EventHandler<DotToDotRound>? RoundReady;

    public async Task LoadAsync()
    {
        var session = _flow.PendingSession;
        if (session is null || session.GameId != GameCatalog.DotToDot)
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

        var round = DotToDotRound.ForBand(player.Band);
        _round = round;

        Total = round.Total;
        Completed = 0;
        ShowsProgressCounter = DifficultyProfile.For(player.Band).UsesWrittenText;

        Pips.Clear();
        for (var i = 0; i < round.Total; i++)
        {
            Pips.Add(new ProgressPip());
        }

        ShowCurrentPicture();

        _clock.Restart();
        RoundReady?.Invoke(this, round);
    }

    private void ShowCurrentPicture()
    {
        if (_round is not { } round)
        {
            return;
        }

        PictureName = DotContent.Name(round.Current.Id);
        PictureGlyph = DotContent.Glyph(round.Current.Id);
        UpdateNextNumber();
    }

    private void UpdateNextNumber() =>
        NextNumber = _round is { } round && !round.IsComplete
            ? (round.NextDot + 1).ToString(LocalizationService.Instance.Culture)
            : string.Empty;

    /// <summary>Çizim yüzeyinden gelen dokunma olayı.</summary>
    public void OnTapped(DotTapResult result)
    {
        if (_round is not { } round)
        {
            return;
        }

        switch (result)
        {
            case DotTapResult.Connected:
                _ = _feedback.PlayAsync(FeedbackCue.Tap);
                UpdateNextNumber();
                break;

            case DotTapResult.Wrong:
                _ = _feedback.PlayAsync(FeedbackCue.Retry);
                break;

            case DotTapResult.PictureComplete:
                _ = _feedback.PlayAsync(FeedbackCue.Correct);

                Completed = round.Completed;

                for (var i = 0; i < Pips.Count; i++)
                {
                    Pips[i].IsFilled = i < round.Completed;
                }

                if (!round.IsComplete)
                {
                    ShowCurrentPicture();
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

    /// <summary>Bütün resimler çizildi.</summary>
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
            GameCatalog.DotToDot,
            player.ProfileId,
            player.Band,
            // Bu oyunda kaybetme yok: yanlış noktaya dokunmak turu bitirmiyor,
            // yalnızca Meşe'de yıldızı etkiliyor.
            Completed: true,
            Correct: round.Completed,
            Mistakes: round.Mistakes,
            Elapsed: _clock.Elapsed,
            // Hedef süre yok: sırayı hızlı takip etmek daha iyi takip etmek değil.
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
                GameCatalog.DotToDot,
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
