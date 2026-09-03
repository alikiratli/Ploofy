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

/// <summary>Ekrandaki tek kutu.</summary>
public sealed partial class CategoryBin(string categoryId, int hueIndex) : ObservableObject
{
    public string CategoryId { get; } = categoryId;

    /// <summary>Kutunun rengi — paletteki sıra.</summary>
    public int HueIndex { get; } = hueIndex;

    /// <summary>Kutunun simgesi. Okuma bilmeyen çocuk kutuyu bundan tanıyor.</summary>
    public string Glyph => CategoryContent.Glyph(CategoryId);

    /// <summary>Kutunun adı; yalnızca okuyan bantta gösteriliyor.</summary>
    public string Name => CategoryContent.Name(CategoryId);

    /// <summary>
    /// Son dokunuşun sonucu — kutu kendi içinde yeşile ya da turuncuya dönüyor.
    /// </summary>
    /// <remarks>
    /// Geri bildirim kutunun üstünde, parçanın değil: çocuğun bakışı
    /// dokunduğu yerde ve "orası değil"i orada görmesi gerekiyor.
    /// </remarks>
    [ObservableProperty]
    public partial GlyphTileStateVm State { get; set; } = GlyphTileStateVm.Idle;
}

/// <summary>Kutunun geri bildirim durumu — <c>Ploofy.Ui</c>'deki karşılığının aynası.</summary>
/// <remarks>
/// Görünüm modeli <c>Ploofy.Ui</c>'ye bağlı değil; aynı üç değer burada da
/// duruyor ki XAML tetikleyicileri bunu bağlayabilsin.
/// </remarks>
public enum GlyphTileStateVm
{
    Idle,
    Correct,
    Wrong,
}

/// <summary>
/// Kategori Ayırma'nın ekranı.
/// </summary>
/// <remarks>
/// Kurallar <see cref="CategorySortRound"/>. Burada kalan iş: ekrandaki
/// parçayı ve kutuları göstermek, dokunuşu motora iletmek ve turun bitişini
/// ilerleme kaydına bağlamak.
/// </remarks>
public sealed partial class CategorySortViewModel : ObservableObject, IDisposable
{
    /// <summary>Yanlış kutunun kırmızı kaldığı süre.</summary>
    private static readonly TimeSpan FeedbackHold = TimeSpan.FromMilliseconds(450);

    private readonly ProgressRepository _repository;
    private readonly PlayFlow _flow;
    private readonly IFeedbackService _feedback;

    private readonly Stopwatch _clock = new();
    private readonly List<PlayerResult> _results = [];

    private TurnController? _controller;
    private CategorySortRound? _round;

    /// <summary>
    /// Sayfa kapanınca bekleyen geri bildirimleri iptal ediyor.
    /// </summary>
    /// <remarks>
    /// Kapanmış bir ekranda dolan bir <c>Task.Delay</c>, geri dönmüş
    /// sayfanın kutusunu sıfırlıyordu. Aynı tuzak Sırayı Tekrarla'da da
    /// vardı — bkz. ilerleme notu, "Tekrar tuzağa düşmemek için".
    /// </remarks>
    private CancellationTokenSource _lifetime = new();

    public CategorySortViewModel(
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
        CurrentGlyph = string.Empty;
        NextGlyph = string.Empty;
    }

    [ObservableProperty]
    public partial string PlayerName { get; set; }

    [ObservableProperty]
    public partial string PlayerAvatar { get; set; }

    /// <summary>Ayrılacak parça — ekranın ortasında, büyük.</summary>
    [ObservableProperty]
    public partial string CurrentGlyph { get; set; }

    /// <summary>
    /// Sıradaki parça, arkada soluk.
    /// </summary>
    /// <remarks>
    /// Şekil Ayırma'daki ile aynı: sıranın devam ettiğini göstermek, turu
    /// bir kuyruk gibi okutuyor ve son parçanın ne zaman geldiğini belli
    /// ediyor.
    /// </remarks>
    [ObservableProperty]
    public partial string NextGlyph { get; set; }

    [ObservableProperty]
    public partial bool HasNext { get; set; }

    /// <summary>Kutuların altında adları da yazılsın mı? Yalnızca okuyan bantta.</summary>
    [ObservableProperty]
    public partial bool ShowsBinNames { get; set; }

    [ObservableProperty]
    public partial bool ShowsProgressCounter { get; set; }

    [ObservableProperty]
    public partial int Sorted { get; set; }

    [ObservableProperty]
    public partial int Total { get; set; }

    [ObservableProperty]
    public partial bool ShowsHandoff { get; set; }

    [ObservableProperty]
    public partial string HandoffText { get; set; }

    public ObservableCollection<CategoryBin> Bins { get; } = [];

    public ObservableCollection<ProgressPip> Pips { get; } = [];

    public async Task LoadAsync()
    {
        var session = _flow.PendingSession;
        if (session is null || session.GameId != GameCatalog.CategorySort)
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

        var round = CategorySortRound.ForBand(player.Band);
        _round = round;

        Total = round.Total;
        Sorted = 0;

        var profile = DifficultyProfile.For(player.Band);
        ShowsProgressCounter = profile.UsesWrittenText;
        ShowsBinNames = profile.UsesWrittenText;

        Bins.Clear();
        for (var i = 0; i < round.Bins.Count; i++)
        {
            Bins.Add(new CategoryBin(round.Bins[i], i));
        }

        Pips.Clear();
        for (var i = 0; i < round.Total; i++)
        {
            Pips.Add(new ProgressPip());
        }

        ShowCurrentItem();
        _clock.Restart();
    }

    private void ShowCurrentItem()
    {
        if (_round is not { } round)
        {
            return;
        }

        CurrentGlyph = round.Current?.Glyph ?? string.Empty;
        NextGlyph = round.Next?.Glyph ?? string.Empty;
        HasNext = round.Next is not null;
    }

    /// <summary>
    /// Bir kutuya dokunuldu.
    /// </summary>
    /// <remarks>
    /// Doğru kutuda parça hemen sıradakine geçiyor; yanlışta parça yerinde
    /// kalıyor ve kutu kısa süre turuncu yanıyor. Bekleme iptal edilebilir:
    /// sayfa kapanırsa geri bildirim sıfırlaması boşa düşüyor.
    /// </remarks>
    [RelayCommand]
    private async Task DropAsync(CategoryBin? bin)
    {
        if (_round is not { } round || bin is null || round.IsComplete)
        {
            return;
        }

        var outcome = round.Drop(bin.CategoryId);

        switch (outcome)
        {
            case DropOutcome.Sorted:
                bin.State = GlyphTileStateVm.Correct;
                await _feedback.PlayAsync(FeedbackCue.Correct);

                Sorted = round.Sorted;
                for (var i = 0; i < Pips.Count; i++)
                {
                    Pips[i].IsFilled = i < round.Sorted;
                }

                ShowCurrentItem();
                break;

            case DropOutcome.WrongBin:
                bin.State = GlyphTileStateVm.Wrong;
                await _feedback.PlayAsync(FeedbackCue.Retry);
                break;

            default:
                return;
        }

        await ClearFeedbackLaterAsync(bin);

        if (round.IsComplete)
        {
            await CompleteRoundAsync();
        }
    }

    private async Task ClearFeedbackLaterAsync(CategoryBin bin)
    {
        try
        {
            await Task.Delay(FeedbackHold, _lifetime.Token);
            bin.State = GlyphTileStateVm.Idle;
        }
        catch (OperationCanceledException)
        {
            // Sayfa kapandı; sıfırlamaya gerek yok.
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
            GameCatalog.CategorySort,
            player.ProfileId,
            player.Band,
            // Kaybetme yok: yanlış kutu turu bitirmiyor, parça yerinde
            // kalıyor ve yalnızca yıldızı etkiliyor.
            Completed: true,
            Correct: round.Sorted,
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
                GameCatalog.CategorySort,
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
