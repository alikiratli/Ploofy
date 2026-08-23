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

namespace Ploofy.App.ViewModels;

/// <summary>Tahtadaki tek kart.</summary>
public sealed partial class MemoryCardVm(int index, string symbol) : ObservableObject
{
    public int Index { get; } = index;

    public string Symbol { get; } = symbol;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(Face))]
    [NotifyPropertyChangedFor(nameof(IsFaceDown))]
    public partial bool IsRevealed { get; set; }

    [ObservableProperty]
    public partial bool IsMatched { get; set; }

    /// <summary>Kapalı kartta sembol görünmüyor — ezberlenecek şey bu.</summary>
    public string Face => IsRevealed ? Symbol : string.Empty;

    public bool IsFaceDown => !IsRevealed;
}

/// <summary>
/// Eşleştirme Kartları oyununun ekranı.
/// </summary>
/// <remarks>
/// <para>
/// Kuralların tamamı <see cref="MemoryMatchRound"/> içinde; burada yalnızca
/// kuralların ekrana yansıması var. Bu ayrım sayesinde oyunun mantığı MAUI
/// olmadan test ediliyor ve bu sınıf kısa kalıyor.
/// </para>
/// <para>
/// Sıra yönetimi de burada değil, <see cref="TurnController"/> içinde. Tek
/// kişilik oyun ile sıralı oyun arasındaki tek fark, devir katmanının
/// görünmesi — oyunun kendi kodunda "çok oyunculu mu?" diye bir dallanma yok.
/// </para>
/// </remarks>
public sealed partial class MemoryMatchViewModel(
    ProgressRepository repository,
    PlayFlow flow,
    IFeedbackService feedback) : ObservableObject, IDisposable
{
    /// <summary>
    /// Kart sembolleri. Meşe bandı 10 çift istiyor, havuz bundan geniş olmalı
    /// ki her tur farklı bir tahta çıksın.
    /// </summary>
    private static readonly string[] SymbolPool =
    [
        "🍎", "🍌", "🍇", "🍓", "🌻", "🌳", "⭐", "🌈",
        "🐝", "🐞", "🦋", "🐟", "🚗", "⚽", "🎈", "🥁",
    ];

    private TurnController? _controller;
    private MemoryMatchRound? _round;
    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];
    private bool _isResolving;

    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [ObservableProperty]
    public partial string PlayerAvatar { get; set; }

    [ObservableProperty]
    public partial int Columns { get; set; }

    [ObservableProperty]
    public partial int MatchedPairs { get; set; }

    [ObservableProperty]
    public partial int TotalPairs { get; set; }

    /// <summary>Sıralı oyunda "cihazı kardeşine ver" katmanı.</summary>
    [ObservableProperty]
    public partial bool ShowsHandoff { get; set; }

    [ObservableProperty]
    public partial string HandoffText { get; set; }

    /// <summary>Meşe bandında süre görünüyor, küçük bantlarda görünmüyor.</summary>
    [ObservableProperty]
    public partial bool ShowsProgressCounter { get; set; }

    public ObservableCollection<MemoryCardVm> Cards { get; } = [];

    public async Task LoadAsync()
    {
        var session = flow.PendingSession;
        if (session is null || session.GameId != GameCatalog.MemoryMatch)
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
                _ = feedback.PlayAsync(FeedbackCue.Handoff);
                break;

            case TurnPhase.Playing:
                ShowsHandoff = false;
                StartRoundFor(state.CurrentPlayer!);
                break;
        }
    }

    /// <summary>
    /// Sıradaki oyuncu için yeni bir tahta kurar.
    /// </summary>
    /// <remarks>
    /// Tahta <b>o oyuncunun</b> bandına göre kuruluyor: küçük kardeş üç çiftle,
    /// büyük kardeş on çiftle aynı oyunu oynayabiliyor. Puan karşılaştırması
    /// yıldızdan ayrı tutulduğu için bu, sıralamayı bozmuyor.
    /// </remarks>
    private void StartRoundFor(Player player)
    {
        PlayerName = player.DisplayName;
        PlayerAvatar = player.AvatarId;

        _round = MemoryMatchRound.ForBand(player.Band, SymbolPool);
        Columns = _round.Columns;
        TotalPairs = _round.TotalPairs;
        MatchedPairs = 0;
        ShowsProgressCounter = DifficultyProfile.For(player.Band).UsesWrittenText;

        Cards.Clear();
        foreach (var card in _round.Cards)
        {
            Cards.Add(new MemoryCardVm(card.Index, card.SymbolId));
        }

        _isResolving = false;
        _clock.Restart();
    }

    [RelayCommand]
    private async Task ConfirmHandoffAsync()
    {
        if (_controller is not null)
        {
            await feedback.PlayAsync(FeedbackCue.Tap);
            await _controller.ConfirmHandoffAsync();
        }
    }

    [RelayCommand]
    private async Task FlipAsync(MemoryCardVm? card)
    {
        if (card is null || _round is null || _controller is null)
        {
            return;
        }

        // Eşleşmeyen çift kapanırken gelen dokunuşlar yok sayılıyor; aksi
        // halde çocuk hızlı hızlı dokununca tahta kilitleniyor.
        if (_isResolving)
        {
            return;
        }

        var result = _round.Flip(card.Index);
        if (result == FlipResult.Ignored)
        {
            return;
        }

        card.IsRevealed = true;
        await _controller.SendMoveAsync(new Dictionary<string, object?> { ["flip"] = card.Index });

        switch (result)
        {
            case FlipResult.AwaitingSecond:
                await feedback.PlayAsync(FeedbackCue.Tap);
                break;

            case FlipResult.Matched:
                await feedback.PlayAsync(FeedbackCue.Correct);
                MarkMatched();
                MatchedPairs = _round.MatchedPairs;

                if (_round.IsComplete)
                {
                    await CompleteRoundAsync();
                }

                break;

            case FlipResult.Mismatched:
                await feedback.PlayAsync(FeedbackCue.Retry);
                await CloseMismatchAsync();
                break;
        }
    }

    private void MarkMatched()
    {
        if (_round is null)
        {
            return;
        }

        foreach (var card in Cards)
        {
            if (_round.MatchedIndices.Contains(card.Index))
            {
                card.IsMatched = true;
            }
        }
    }

    private async Task CloseMismatchAsync()
    {
        if (_round is null)
        {
            return;
        }

        _isResolving = true;

        // Açık kalma süresi banda bağlı: küçük yaşta kartın kapanma hızı
        // oyunun asıl zorluğu.
        await Task.Delay(_round.MismatchReveal);

        var open = _round.FaceUpIndices.ToArray();
        _round.CloseMismatch();

        foreach (var index in open)
        {
            var card = Cards.FirstOrDefault(c => c.Index == index);
            if (card is not null)
            {
                card.IsRevealed = false;
            }
        }

        _isResolving = false;
    }

    private async Task CompleteRoundAsync()
    {
        if (_round is null || _controller is null)
        {
            return;
        }

        _clock.Stop();
        await feedback.PlayAsync(FeedbackCue.RoundComplete);

        var player = _controller.State.CurrentPlayer!;
        var outcome = new RoundOutcome(
            GameCatalog.MemoryMatch,
            player.ProfileId,
            player.Band,
            Completed: true,
            Correct: _round.TotalPairs,
            Mistakes: _round.Mistakes,
            Elapsed: _clock.Elapsed,
            ParTime: _round.ParTime);

        var stars = await repository.RecordRoundAsync(outcome);
        var score = StarRating.RawScore(outcome);

        _results.Add(new PlayerResult(player.DisplayName, player.AvatarId, stars, score));

        // Yıldız kutlaması turu bitiren çocuğa ait; sıra devretmeden önce.
        if (stars > 0)
        {
            await feedback.PlayAsync(FeedbackCue.StarEarned);
        }

        await _controller.FinishTurnAsync(score);

        if (_controller.State.Phase == TurnPhase.Finished)
        {
            flow.LastSummary = new RoundSummary(
                GameCatalog.MemoryMatch,
                _results.ToList(),
                _controller.Session.IsMultiplayer);

            await Shell.Current.GoToAsync("result");
        }
    }

    /// <summary>Oyundan çıkış — yarım kalan tur kaydedilmiyor.</summary>
    [RelayCommand]
    private async Task QuitAsync() => await Shell.Current.GoToAsync("..");

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
