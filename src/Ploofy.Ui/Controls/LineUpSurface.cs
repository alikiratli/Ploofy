using Ploofy.Engine.Games;
using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>Bir parçanın yuvaya bırakılmasının sonucu.</summary>
public sealed class LineUpPlaceEventArgs(PlaceOutcome outcome) : EventArgs
{
    public PlaceOutcome Outcome { get; } = outcome;
}

/// <summary>
/// Sırala'nın sürükleme yüzeyi.
/// </summary>
/// <remarks>
/// <para>
/// Çizimin ve yerleşimin tamamı <see cref="LineUpPainter"/> ile
/// <see cref="LineUpLayout"/> içinde ve MAUI'den bağımsız; burada kalan iş
/// parmağı takip etmek, animasyonları yürütmek ve motora haber vermek.
/// Ayrılığın sebebi, yerleşimin ekrana bakmadan doğrulanamaması.
/// </para>
/// <para>
/// Sürükleme için MAUI'nin sürükle-bırak tanıyıcıları değil doğrudan dokunma
/// olayları kullanılıyor: platformun sürükleme eşiği küçük çocuğun yavaş
/// hareketinde aşılmıyor ve parça hiç kıpırdamıyor.
/// </para>
/// </remarks>
public sealed class LineUpSurface : SKCanvasView
{
    private const float ReturnDuration = 0.28f;
    private const float ShakeDuration = 0.4f;

    /// <summary>Tamamlanmış dizinin ekranda kaldığı süre.</summary>
    /// <remarks>
    /// Motor kendiliğinden sonraki bulmacaya geçmiyor; bu payın sonunda
    /// <see cref="LineUpRound.NextPuzzle"/> çağrılıyor. Pay olmadan çocuk
    /// tamamladığı diziyi hiç görmüyor.
    /// </remarks>
    private const float CelebrationDuration = 0.9f;

    private readonly LineUpPainter _painter = new();
    private readonly ParticleField _particles = new() { Gravity = 620f };
    private readonly Random _rng = new();

    private IDispatcherTimer? _ticker;
    private DateTime _lastFrame;
    private float _time;

    // Sürükleme durumu
    private LineUpPiece? _dragged;
    private SKPoint _dragPoint;
    private SKPoint _grabOffset;
    private int _hoveredSlot = -1;

    // Animasyon durumu
    private LineUpPiece? _returning;
    private SKPoint _returnFrom;
    private float _returnStartedAt = float.MinValue;
    private float _shakeStartedAt = float.MinValue;
    private float _solvedAt = float.MinValue;

    /// <summary>Son çizimde hesaplanan yerleşim; dokunma isabeti bunu kullanıyor.</summary>
    private LineUpLayout? _layout;

    public LineUpSurface()
    {
        EnableTouchEvents = true;
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
        Touch += OnTouch;
    }

    public LineUpRound? Round { get; private set; }

    /// <summary>Bir parça yuvaya bırakıldı (doğru ya da yanlış).</summary>
    public event EventHandler<LineUpPlaceEventArgs>? Placed;

    /// <summary>Bir bulmaca tamamlandı.</summary>
    public event EventHandler? PuzzleSolved;

    /// <summary>Bütün bulmacalar çözüldü.</summary>
    public event EventHandler? RoundOver;

    public void Start(LineUpRound round)
    {
        Round = round;
        _time = 0f;
        _dragged = null;
        _returning = null;
        _hoveredSlot = -1;
        _returnStartedAt = float.MinValue;
        _shakeStartedAt = float.MinValue;
        _solvedAt = float.MinValue;
        _particles.Clear();

        _lastFrame = DateTime.UtcNow;
        StartTicker();
        InvalidateSurface();
    }

    public void Stop()
    {
        _ticker?.Stop();
        _ticker = null;
    }

    /// <summary>Kutlama sürerken dokunuş kapalı: sıradaki bulmaca henüz gelmedi.</summary>
    private bool IsCelebrating => _time - _solvedAt < CelebrationDuration;

    private void StartTicker()
    {
        if (_ticker is not null)
        {
            _ticker.Start();
            return;
        }

        _ticker = Dispatcher.CreateTimer();
        _ticker.Interval = TimeSpan.FromMilliseconds(16);
        _ticker.Tick += (_, _) =>
        {
            var now = DateTime.UtcNow;
            _time += MathF.Min((float)(now - _lastFrame).TotalSeconds, 0.05f);
            _lastFrame = now;

            _particles.Advance(1f / 60f);

            if (_returning is not null && _time - _returnStartedAt >= ReturnDuration)
            {
                _returning = null;
            }

            AdvanceCelebration();
            InvalidateSurface();
        };
        _ticker.Start();
    }

    /// <summary>Kutlama bitince sıradaki bulmacayı ister ya da turu kapatır.</summary>
    private void AdvanceCelebration()
    {
        if (Round is not { } round || _solvedAt == float.MinValue || IsCelebrating)
        {
            return;
        }

        _solvedAt = float.MinValue;

        if (round.IsComplete)
        {
            RoundOver?.Invoke(this, EventArgs.Empty);
            return;
        }

        round.NextPuzzle();
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        e.Handled = true;

        if (Round is not { } round || round.IsComplete || IsCelebrating || _layout is null)
        {
            return;
        }

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                Grab(round, e.Location);
                break;

            case SKTouchAction.Moved when _dragged is not null:
                _dragPoint = e.Location;
                _hoveredSlot = _layout.SlotAt(DragCenter());
                break;

            case SKTouchAction.Released or SKTouchAction.Cancelled when _dragged is not null:
                Release(round);
                break;
        }

        InvalidateSurface();
    }

    /// <summary>
    /// Parmağın altındaki tepsi parçasını tutar.
    /// </summary>
    /// <remarks>
    /// Parçanın yakınına dokunmak da yetiyor: küçük bir çocuğun parmağı
    /// büyük, isabeti düşük. En yakın parça seçiliyor ki iki parça yan yana
    /// olduğunda tutulan hep niyet edilen olsun.
    /// </remarks>
    private void Grab(LineUpRound round, SKPoint at)
    {
        var layout = _layout!;
        var reach = layout.PieceRadius * 1.7f;

        var best = -1;
        var bestDistance = reach * reach;

        for (var i = 0; i < round.Tray.Count && i < layout.Tray.Count; i++)
        {
            var dx = at.X - layout.Tray[i].X;
            var dy = at.Y - layout.Tray[i].Y;
            var distance = (dx * dx) + (dy * dy);

            if (distance <= bestDistance)
            {
                bestDistance = distance;
                best = i;
            }
        }

        if (best < 0)
        {
            return;
        }

        _dragged = round.Tray[best];
        _dragPoint = at;

        // Parça parmağın altına sıçramasın: tutulan nokta korunuyor.
        _grabOffset = new SKPoint(layout.Tray[best].X - at.X, layout.Tray[best].Y - at.Y);
        _returning = null;
        _returnStartedAt = float.MinValue;
    }

    private SKPoint DragCenter() =>
        new(_dragPoint.X + _grabOffset.X, _dragPoint.Y + _grabOffset.Y);

    private void Release(LineUpRound round)
    {
        var piece = _dragged!;
        var center = DragCenter();

        _dragged = null;
        _hoveredSlot = -1;

        var slotIndex = _layout!.SlotAt(center);
        if (slotIndex < 0)
        {
            // Boşluğa bırakıldı: sessizce yerine dönüyor, hata sayılmıyor.
            StartReturn(piece, center);
            return;
        }

        var outcome = round.Place(piece.Id, slotIndex);

        switch (outcome)
        {
            case PlaceOutcome.Fitted:
                _particles.Burst(
                    _layout.Slots[slotIndex],
                    _layout.PieceRadius * 0.5f,
                    PloofyPalette.For(piece.Hue),
                    _rng,
                    count: 18);

                if (round.PuzzleSolved)
                {
                    _solvedAt = _time;
                    PuzzleSolved?.Invoke(this, EventArgs.Empty);
                }

                break;

            case PlaceOutcome.WrongSlot:
                _shakeStartedAt = _time;
                StartReturn(piece, center);
                break;

            default:
                StartReturn(piece, center);
                break;
        }

        Placed?.Invoke(this, new LineUpPlaceEventArgs(outcome));
    }

    private void StartReturn(LineUpPiece piece, SKPoint from)
    {
        _returning = piece;
        _returnFrom = from;
        _returnStartedAt = _time;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var width = e.Info.Width;
        var height = e.Info.Height;

        _painter.DrawBackground(canvas, width, height);

        if (Round is not { } round)
        {
            return;
        }

        var layout = LineUpLayout.For(width, height, round.Slots.Count, round.Tray.Count);
        _layout = layout;

        _painter.DrawDirectionHint(canvas, layout, round.Direction);
        _painter.DrawSlots(canvas, layout, round, _hoveredSlot);
        _painter.DrawTray(canvas, layout, round, _dragged ?? _returning);

        DrawReturning(canvas, layout, round);

        if (_dragged is { } dragged)
        {
            _painter.DrawPiece(
                canvas, layout, dragged, DragCenter(), round.Attribute, scale: 1.08f);
        }

        _particles.Draw(canvas);
    }

    private void DrawReturning(SKCanvas canvas, LineUpLayout layout, LineUpRound round)
    {
        if (_returning is not { } piece)
        {
            return;
        }

        var index = IndexInTray(round, piece);
        if (index < 0 || index >= layout.Tray.Count)
        {
            return;
        }

        var t = Math.Clamp((_time - _returnStartedAt) / ReturnDuration, 0f, 1f);
        var eased = 1f - ((1f - t) * (1f - t));

        var home = layout.Tray[index];
        var center = new SKPoint(
            _returnFrom.X + ((home.X - _returnFrom.X) * eased),
            _returnFrom.Y + ((home.Y - _returnFrom.Y) * eased));

        // Yanlış yuvadan dönen parça yolda silkeleniyor: "bu değil".
        if (_time - _shakeStartedAt < ShakeDuration)
        {
            center.X += MathF.Sin((_time - _shakeStartedAt) * 46f) * layout.PieceRadius * 0.18f;
        }

        _painter.DrawPiece(canvas, layout, piece, center, round.Attribute);
    }

    private static int IndexInTray(LineUpRound round, LineUpPiece piece)
    {
        for (var i = 0; i < round.Tray.Count; i++)
        {
            if (round.Tray[i].Id == piece.Id)
            {
                return i;
            }
        }

        return -1;
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null)
        {
            return;
        }

        Stop();
        _painter.Dispose();
        _particles.Dispose();
    }
}
