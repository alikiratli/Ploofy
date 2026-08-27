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

/// <summary>Ekrandaki tek tuş.</summary>
public sealed partial class SimonPadVm(int index, string symbol) : ObservableObject
{
    public int Index { get; } = index;

    /// <summary>Rengi de şekli de tuş sırasından geliyor; ikisi hep birlikte.</summary>
    public int HueIndex { get; } = index;

    public string Symbol { get; } = symbol;

    [ObservableProperty]
    public partial SimonPadState State { get; set; }
}

/// <summary>
/// Sırayı Tekrarla'nın ekranı.
/// </summary>
/// <remarks>
/// <para>
/// Kurallar <see cref="SimonRound"/> içinde; <b>zamanlama burada</b>. Bu oyun
/// kütüphanedeki diğerlerinden bu yüzden ayrı duruyor: ekran kendi kendine
/// bir şey oynatıyor ve o sırada çocuğun dokunuşu kapalı. Motorun bir
/// "gösterim" durumu yok, çünkü gösterim tamamen arayüzün işi.
/// </para>
/// <para>
/// Gösterim iptal edilebilir olmak zorunda: sayfa kapanırken ya da sıra
/// kardeşe geçerken yarım kalan bir gösterim, kapanmış bir ekranda tuş
/// yakmaya devam ediyordu.
/// </para>
/// </remarks>
public sealed partial class SimonViewModel : ObservableObject, IDisposable
{
    /// <summary>Tuşların taşıdığı şekiller — Şekil Ayırma'nın sözlüğü.</summary>
    private static readonly string[] Symbols = ["●", "▲", "■", "★", "♥", "◆"];

    /// <summary>Gösterim başlamadan önceki hazırlık payı.</summary>
    /// <remarks>Çocuk sırasını verdikten sonra ekrana dönene kadar geçen an.</remarks>
    private static readonly TimeSpan LeadIn = TimeSpan.FromMilliseconds(700);

    /// <summary>Dokunulan tuşun yanık kaldığı süre.</summary>
    private static readonly TimeSpan TapBlink = TimeSpan.FromMilliseconds(220);

    /// <summary>Yanlıştan sonra dizi yeniden gösterilmeden önceki bekleme.</summary>
    private static readonly TimeSpan WrongPause = TimeSpan.FromMilliseconds(700);

    /// <summary>Seviye bitince kutlamanın görülmesi için bekleme.</summary>
    private static readonly TimeSpan LevelPause = TimeSpan.FromMilliseconds(650);

    private readonly ProgressRepository _repository;
    private readonly PlayFlow _flow;
    private readonly IFeedbackService _feedback;

    private TurnController? _controller;
    private SimonRound? _round;

    /// <summary>Sayfanın ömrü. Kapanınca bekleyen her gecikme buradan kesiliyor.</summary>
    /// <remarks>
    /// Yalnızca gösterimi iptal etmek yetmiyordu: yanlıştan sonraki bekleme
    /// sayfa kapandıktan sonra da doluyor ve kapanmış ekranda yeni bir
    /// gösterim başlatıyordu.
    /// </remarks>
    private readonly CancellationTokenSource _life = new();
    private CancellationTokenSource? _playback;
    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];
    private bool _isResolving;

    public SimonViewModel(
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
        StatusText = string.Empty;
        StatusGlyph = string.Empty;
    }

    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [ObservableProperty]
    public partial string PlayerAvatar { get; set; }

    /// <summary>Ekran diziyi gösteriyor mu? Doğruysa dokunuş kapalı.</summary>
    [ObservableProperty]
    public partial bool IsWatching { get; set; }

    /// <summary>Yönergenin simgesi — okuma bilmeyen bant için tek işaret bu.</summary>
    [ObservableProperty]
    public partial string StatusGlyph { get; set; }

    [ObservableProperty]
    public partial string StatusText { get; set; }

    [ObservableProperty]
    public partial bool ShowsProgressCounter { get; set; }

    [ObservableProperty]
    public partial int Completed { get; set; }

    [ObservableProperty]
    public partial int Total { get; set; }

    /// <summary>Tuşlar kaç sütuna dizilecek.</summary>
    [ObservableProperty]
    public partial int Columns { get; set; } = 3;

    [ObservableProperty]
    public partial bool ShowsHandoff { get; set; }

    [ObservableProperty]
    public partial string HandoffText { get; set; }

    public ObservableCollection<SimonPadVm> Pads { get; } = [];

    public ObservableCollection<ProgressPip> Pips { get; } = [];

    public async Task LoadAsync()
    {
        var session = _flow.PendingSession;
        if (session is null || session.GameId != GameCatalog.SimonSequence)
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
                // Devir katmanı ekranı kapatıyor; arkada süren gösterim
                // kimsenin görmediği tuşları yakmaya devam ederdi.
                CancelPlayback();
                ShowsHandoff = true;
                HandoffText = LocalizationService.Instance.Format(
                    "HandoffTitle", state.CurrentPlayer!.DisplayName);
                _ = _feedback.PlayAsync(FeedbackCue.Handoff);
                break;

            case TurnPhase.Playing:
                ShowsHandoff = false;
                _ = StartRoundForAsync(state.CurrentPlayer!);
                break;
        }
    }

    private async Task StartRoundForAsync(Player player)
    {
        PlayerName = player.DisplayName;
        PlayerAvatar = player.AvatarId;

        var round = SimonRound.ForBand(player.Band);
        _round = round;

        Total = round.Total;
        Completed = 0;
        ShowsProgressCounter = DifficultyProfile.For(player.Band).UsesWrittenText;

        Pads.Clear();
        for (var i = 0; i < round.Pads; i++)
        {
            Pads.Add(new SimonPadVm(i, Symbols[i % Symbols.Length]));
        }

        // Dört tuş 2x2, üç ve altı tuş üçerli. Dördü tek sıraya dizmek
        // tablette tuşları basık çubuklara çeviriyor.
        Columns = round.Pads == 4 ? 2 : 3;

        Pips.Clear();
        for (var i = 0; i < round.Total; i++)
        {
            Pips.Add(new ProgressPip());
        }

        _isResolving = false;
        _clock.Restart();

        await ShowSequenceAsync();
    }

    /// <summary>Diziyi bir kez oynatır ve sırayı çocuğa bırakır.</summary>
    private async Task ShowSequenceAsync()
    {
        if (_round is not { } round)
        {
            return;
        }

        CancelPlayback();

        if (_life.IsCancellationRequested)
        {
            return;
        }

        var cts = CancellationTokenSource.CreateLinkedTokenSource(_life.Token);
        _playback = cts;
        var token = cts.Token;

        SetWatching(true);
        foreach (var pad in Pads)
        {
            pad.State = SimonPadState.Idle;
        }

        // Aradaki karanlık aralık, aynı tuşun peş peşe iki kez yanmasını
        // ayırt edilebilir kılan tek şey — dizide bu ancak Meşe'de oluyor
        // ama aralık her bantta duruyor, ritmi de o veriyor.
        var gap = round.StepDuration * 0.4;
        var sequence = round.Sequence.ToList();

        try
        {
            await Task.Delay(LeadIn, token);

            foreach (var pad in sequence)
            {
                Pads[pad].State = SimonPadState.Lit;

                // Her tuşun kendi notası var, dolayısıyla gösterim bir ezgi.
                // Filiz bandındaki çocuk "üçüncü, sonra birinci" diye
                // düşünemiyor ama üç notalık bir ezgiyi tekrarlayabiliyor.
                _ = _feedback.PlayAsync(FeedbackCues.Pad(pad));
                await Task.Delay(round.StepDuration, token);

                Pads[pad].State = SimonPadState.Idle;
                await Task.Delay(gap, token);
            }

            SetWatching(false);
        }
        catch (OperationCanceledException)
        {
            // Sayfa kapandı, sıra devredildi ya da yeni gösterim başladı.
        }
    }

    private void SetWatching(bool isWatching)
    {
        IsWatching = isWatching;
        StatusGlyph = isWatching ? "👀" : "👉";
        StatusText = LocalizationService.Instance[isWatching ? "SimonWatch" : "SimonRepeat"];
    }

    [RelayCommand]
    private async Task TapAsync(SimonPadVm? pad)
    {
        if (pad is null || _round is null || IsWatching || _isResolving)
        {
            return;
        }

        var outcome = _round.Tap(pad.Index);
        if (outcome == SimonOutcome.Ignored)
        {
            return;
        }

        _isResolving = true;
        try
        {
            switch (outcome)
            {
                case SimonOutcome.Correct:
                    // Gösterimde duyduğu notanın aynısı: çocuk çaldığı ezgiyi
                    // duyduğuyla karşılaştırabilsin.
                    await BlinkAsync(pad, FeedbackCues.Pad(pad.Index));
                    break;

                case SimonOutcome.Wrong:
                    pad.State = SimonPadState.Wrong;
                    await _feedback.PlayAsync(FeedbackCue.Retry);
                    await Task.Delay(WrongPause, _life.Token);
                    pad.State = SimonPadState.Idle;

                    // Aynı dizi bir kez daha gösteriliyor; yeni bir dizi
                    // üretmek çocuğun tam da takıldığı şeyi elinden alırdı.
                    await ShowSequenceAsync();
                    break;

                case SimonOutcome.LevelComplete:
                    await BlinkAsync(pad, FeedbackCue.Correct);
                    SyncPips();

                    if (_round.IsComplete)
                    {
                        await CompleteRoundAsync();
                        return;
                    }

                    await Task.Delay(LevelPause, _life.Token);
                    await ShowSequenceAsync();
                    break;
            }
        }
        catch (OperationCanceledException)
        {
            // Sayfa kapandı; yarım kalan geri bildirimin gideceği ekran yok.
        }
        finally
        {
            _isResolving = false;
        }
    }

    private async Task BlinkAsync(SimonPadVm pad, FeedbackCue cue)
    {
        pad.State = SimonPadState.Lit;
        _ = _feedback.PlayAsync(cue);
        await Task.Delay(TapBlink, _life.Token);
        pad.State = SimonPadState.Idle;
    }

    private void SyncPips()
    {
        if (_round is null)
        {
            return;
        }

        Completed = _round.Completed;
        for (var i = 0; i < Pips.Count; i++)
        {
            Pips[i].IsFilled = i < _round.Completed;
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

        CancelPlayback();
        _clock.Stop();
        await _feedback.PlayAsync(FeedbackCue.RoundComplete);

        var player = _controller.State.CurrentPlayer!;
        var outcome = new RoundOutcome(
            GameCatalog.SimonSequence,
            player.ProfileId,
            player.Band,
            // Bu oyunda kaybetme yok: yanlış tuş diziyi bitirmiyor, baştan
            // gösteriyor. Tur ancak bütün seviyeler geçilince bitiyor.
            Completed: true,
            Correct: round.Completed,
            Mistakes: round.Mistakes,
            Elapsed: _clock.Elapsed,
            // Hedef süre yok: turun büyük kısmı ekranın kendi gösterimi ve
            // çocuk onu hızlandıramıyor. Bkz. SimonTuning.
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
                GameCatalog.SimonSequence,
                _results.ToList(),
                _controller.Session.IsMultiplayer);

            await Shell.Current.GoToAsync("result");
        }
    }

    [RelayCommand]
    private static async Task QuitAsync() => await Shell.Current.GoToAsync("..");

    private void CancelPlayback()
    {
        if (_playback is null)
        {
            return;
        }

        _playback.Cancel();
        _playback.Dispose();
        _playback = null;
    }

    public void Dispose()
    {
        _life.Cancel();
        CancelPlayback();
        _life.Dispose();

        if (_controller is not null)
        {
            _controller.StateChanged -= OnTurnStateChanged;
            _ = _controller.DisposeAsync();
            _controller = null;
        }
    }
}
