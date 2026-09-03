using System.Collections.ObjectModel;
using System.Diagnostics;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Ploofy.App.Localization;
using Ploofy.App.Services;
using Ploofy.Data;
using Ploofy.Engine.Catalog;
using Ploofy.Engine.Games;
using Ploofy.Engine.Progress;
using Ploofy.Engine.Sessions;
using Ploofy.Ui.Feedback;
using Ploofy.Ui.Painting;

namespace Ploofy.App.ViewModels;

/// <summary>Paletteki tek renk.</summary>
public sealed partial class PaintColor(int index, Color swatch) : ObservableObject
{
    public int Index { get; } = index;

    public Color Swatch { get; } = swatch;

    [ObservableProperty]
    public partial bool IsSelected { get; set; }

    /// <summary>Seçili renk büyüyor: hangisinin elde olduğu tereddütsüz belli.</summary>
    public double Scale => IsSelected ? 1.18 : 1.0;
}

/// <summary>
/// Boyama'nın ekranı.
/// </summary>
/// <remarks>
/// Kurallar <see cref="ColoringRound"/>, çizim ve dokunma
/// <c>ColoringSurface</c> içinde. Burada kalan iş palet ve ilerleme kaydı.
/// Bu oyunda sayaç, süre ya da hata göstergesi <b>yok</b>: serbest oyunun
/// üstüne konan her ölçüm onu serbest olmaktan çıkarır.
/// </remarks>
public sealed partial class ColoringViewModel : ObservableObject, IDisposable
{
    private readonly ProgressRepository _repository;
    private readonly PlayFlow _flow;
    private readonly IFeedbackService _feedback;

    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];

    private TurnController? _controller;
    private ColoringRound? _round;

    public ColoringViewModel(
        ProgressRepository repository,
        PlayFlow flow,
        IFeedbackService feedback)
    {
        _repository = repository;
        _flow = flow;
        _feedback = feedback;

        PlayerName = string.Empty;
        PlayerAvatar = string.Empty;

        for (var i = 0; i < ColoringTuning.PaletteSize; i++)
        {
            var hue = PloofyPalette.All[i % PloofyPalette.All.Count];
            Palette.Add(new PaintColor(i, ToMaui(hue.Body)) { IsSelected = i == 0 });
        }
    }

    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [ObservableProperty]
    public partial string PlayerAvatar { get; set; }

    public ObservableCollection<PaintColor> Palette { get; } = [];

    public ObservableCollection<ProgressPip> Pips { get; } = [];

    /// <summary>Yeni tur hazır — sayfa çizim yüzeyini bununla başlatıyor.</summary>
    public event EventHandler<ColoringRound>? RoundReady;

    public async Task LoadAsync()
    {
        var session = _flow.PendingSession;
        if (session is null || session.GameId != GameCatalog.Coloring)
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
        if (state.Phase == TurnPhase.Playing)
        {
            StartRoundFor(state.CurrentPlayer!);
        }
    }

    private void StartRoundFor(Player player)
    {
        PlayerName = player.DisplayName;
        PlayerAvatar = player.AvatarId;

        var round = ColoringRound.ForBand(player.Band);
        _round = round;

        // Palet seçimi tur değişince sıfırlanmıyor ama motor yeni turda
        // varsayılana dönüyor; ikisini eşitliyoruz.
        var selected = Palette.FirstOrDefault(c => c.IsSelected) ?? Palette[0];
        round.SelectColor(selected.Index);

        Pips.Clear();
        for (var i = 0; i < round.Total; i++)
        {
            Pips.Add(new ProgressPip());
        }

        _clock.Restart();
        RoundReady?.Invoke(this, round);
    }

    [RelayCommand]
    private async Task PickColorAsync(PaintColor? color)
    {
        if (color is null || _round is null)
        {
            return;
        }

        _round.SelectColor(color.Index);

        foreach (var option in Palette)
        {
            option.IsSelected = ReferenceEquals(option, color);
        }

        await _feedback.PlayAsync(FeedbackCue.Tap);
    }

    /// <summary>Çizim yüzeyinden gelen boyama olayı.</summary>
    public void OnPainted(PaintOutcome outcome)
    {
        if (_round is not { } round)
        {
            return;
        }

        switch (outcome)
        {
            case PaintOutcome.Painted:
                _ = _feedback.PlayAsync(FeedbackCue.Tap);
                break;

            case PaintOutcome.PictureComplete:
                _ = _feedback.PlayAsync(FeedbackCue.Correct);

                for (var i = 0; i < Pips.Count; i++)
                {
                    Pips[i].IsFilled = i < round.Completed;
                }

                break;
        }
    }

    /// <summary>Bütün resimler boyandı.</summary>
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
            GameCatalog.Coloring,
            player.ProfileId,
            player.Band,
            Completed: true,
            Correct: round.Completed,
            // Serbest oyunda hata diye bir şey yok: her boyama doğru.
            Mistakes: 0,
            Elapsed: _clock.Elapsed,
            // Hedef süre de yok — boyamayı hızlı bitirmek daha iyi boyamak değil.
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
                GameCatalog.Coloring,
                _results.ToList(),
                _controller.Session.IsMultiplayer);

            await Shell.Current.GoToAsync("result");
        }
    }

    [RelayCommand]
    private static async Task QuitAsync() => await Shell.Current.GoToAsync("..");

    private static Color ToMaui(SkiaSharp.SKColor color) =>
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
