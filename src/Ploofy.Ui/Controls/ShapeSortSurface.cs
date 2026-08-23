using Ploofy.Engine.Games;
using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>Bir parçanın kutuya bırakılmasının ekrandaki karşılığı.</summary>
public sealed class ShapeDropEventArgs(DropOutcome outcome, ShapeKind bin) : EventArgs
{
    public DropOutcome Outcome { get; } = outcome;

    public ShapeKind Bin { get; } = bin;
}

/// <summary>
/// Şekil Ayırma'nın çizim ve sürükleme yüzeyi.
/// </summary>
/// <remarks>
/// <para>
/// Sürükleme MAUI'nin sürükle-bırak tanıyıcılarıyla değil, doğrudan dokunma
/// olaylarıyla yapılıyor. Sebebi: parçanın parmağı <b>kare kare</b> takip
/// etmesi gerekiyor. Platformun sürükleme katmanı bir "sürükleme başladı"
/// eşiği bekliyor ve küçük çocuğun yavaş, tereddütlü hareketinde bu eşik
/// çoğu zaman hiç aşılmıyor — parça yerinden kıpırdamıyor ve çocuk oyunun
/// bozuk olduğunu sanıyor.
/// </para>
/// <para>
/// Kutu isabet alanı çizilenden geniş: parçayı kutunun tam ortasına
/// bırakmak bu yaşta beklenemez, yakınına bırakmak yeterli olmalı.
/// </para>
/// </remarks>
public sealed class ShapeSortSurface : SKCanvasView
{
    private const float ReturnDuration = 0.28f;
    private const float SettleDuration = 0.22f;
    private const float ShakeDuration = 0.4f;

    private readonly ShapePainter _painter = new();
    private readonly ParticleField _particles = new() { Gravity = 700f };
    private readonly Random _rng = new();

    private readonly SKPaint _background = new() { IsAntialias = true };
    private readonly SKPaint _tray = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    private IDispatcherTimer? _ticker;
    private DateTime _lastFrame;
    private float _time;

    // Sürükleme durumu
    private bool _isDragging;
    private SKPoint _dragPoint;
    private SKPoint _grabOffset;
    private int _hoveredBin = -1;

    // Animasyon durumu
    private SKPoint _returnFrom;
    private float _returnStartedAt = float.MinValue;
    private float _settleStartedAt = float.MinValue;
    private SKPoint _settleFrom;
    private SKPoint _settleTo;
    private ShapePiece? _settlingPiece;
    private float _shakeStartedAt = float.MinValue;

    // Son çizimde hesaplanan yerleşim; dokunma isabeti bunu kullanıyor.
    private SKPoint[] _binCenters = [];
    private float _binRadius;
    private float _trayRadius;
    private SKPoint _pieceHome;
    private float _pieceRadius;

    public ShapeSortSurface()
    {
        EnableTouchEvents = true;
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
        Touch += OnTouch;
    }

    public ShapeSortRound? Round { get; private set; }

    /// <summary>Bir parça kutuya bırakıldı (doğru ya da yanlış).</summary>
    public event EventHandler<ShapeDropEventArgs>? Dropped;

    /// <summary>Bütün parçalar ayrıldı.</summary>
    public event EventHandler? RoundOver;

    public void Start(ShapeSortRound round)
    {
        Round = round;
        _time = 0f;
        _isDragging = false;
        _hoveredBin = -1;
        _settlingPiece = null;
        _returnStartedAt = float.MinValue;
        _settleStartedAt = float.MinValue;
        _shakeStartedAt = float.MinValue;
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

            if (_settlingPiece is not null && _time - _settleStartedAt >= SettleDuration)
            {
                _settlingPiece = null;

                if (Round is { IsComplete: true })
                {
                    RoundOver?.Invoke(this, EventArgs.Empty);
                }
            }

            InvalidateSurface();
        };
        _ticker.Start();
    }

    private void OnTouch(object? sender, SKTouchEventArgs e)
    {
        e.Handled = true;

        if (Round is not { } round || round.IsComplete || _settlingPiece is not null)
        {
            return;
        }

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                // Parçanın yakınına dokunmak da tutmaya yetiyor.
                var grabReach = _pieceRadius * 1.6f;
                var dx = e.Location.X - _pieceHome.X;
                var dy = e.Location.Y - _pieceHome.Y;

                if ((dx * dx) + (dy * dy) <= grabReach * grabReach)
                {
                    _isDragging = true;
                    _dragPoint = e.Location;
                    // Parça parmağın altına sıçramasın: tutulan nokta korunuyor.
                    _grabOffset = new SKPoint(_pieceHome.X - e.Location.X, _pieceHome.Y - e.Location.Y);
                    _returnStartedAt = float.MinValue;
                }

                break;

            case SKTouchAction.Moved when _isDragging:
                _dragPoint = e.Location;
                _hoveredBin = BinAt(DragCenter());
                break;

            case SKTouchAction.Released or SKTouchAction.Cancelled when _isDragging:
                ReleaseAt(DragCenter(), round);
                break;
        }

        InvalidateSurface();
    }

    private SKPoint DragCenter() =>
        new(_dragPoint.X + _grabOffset.X, _dragPoint.Y + _grabOffset.Y);

    private void ReleaseAt(SKPoint center, ShapeSortRound round)
    {
        _isDragging = false;
        _hoveredBin = -1;

        var binIndex = BinAt(center);
        if (binIndex < 0)
        {
            // Boşluğa bırakıldı: sessizce yerine dönüyor, hata sayılmıyor.
            StartReturn(center);
            return;
        }

        var piece = round.Current;
        var bin = round.Bins[binIndex];
        var outcome = round.Drop(bin);

        switch (outcome)
        {
            case DropOutcome.Sorted when piece is not null:
                _settlingPiece = piece;
                _settleFrom = center;
                _settleTo = _binCenters[binIndex];
                _settleStartedAt = _time;

                _particles.Burst(
                    _binCenters[binIndex],
                    _binRadius * 0.8f,
                    PloofyPalette.For(piece.Hue),
                    _rng,
                    count: 12);
                break;

            case DropOutcome.WrongBin:
                _shakeStartedAt = _time;
                StartReturn(center);
                break;

            default:
                StartReturn(center);
                break;
        }

        Dropped?.Invoke(this, new ShapeDropEventArgs(outcome, bin));
    }

    private void StartReturn(SKPoint from)
    {
        _returnFrom = from;
        _returnStartedAt = _time;
    }

    private int BinAt(SKPoint point)
    {
        // İsabet alanı çizilenden geniş: parçayı kutunun tam ortasına
        // bırakmak bu yaşta beklenemez.
        var reach = _binRadius * 1.5f;

        for (var i = 0; i < _binCenters.Length; i++)
        {
            var dx = point.X - _binCenters[i].X;
            var dy = point.Y - _binCenters[i].Y;
            if ((dx * dx) + (dy * dy) <= reach * reach)
            {
                return i;
            }
        }

        return -1;
    }

    private void OnPaintSurface(object? sender, SKPaintSurfaceEventArgs e)
    {
        var canvas = e.Surface.Canvas;
        var width = e.Info.Width;
        var height = e.Info.Height;

        DrawBackground(canvas, width, height);

        if (Round is not { } round)
        {
            return;
        }

        Layout(round, width, height);
        DrawBins(canvas, round);
        DrawPieces(canvas, round);

        _particles.Draw(canvas);
    }

    private void Layout(ShapeSortRound round, float width, float height)
    {
        var count = round.Bins.Count;

        // Tepsi boyutu önce hesaplanıyor, kutu ondan türetiliyor. Tersi
        // yapıldığında (kutudan tepsiye) tepsiler ekranın dışına taşıyor:
        // dört kutuda kenardakilerin bir kısmı görünmüyordu.
        var margin = width * 0.025f;
        var gap = width * 0.012f;
        var slot = (width - (2f * margin)) / count;

        _trayRadius = MathF.Min((slot / 2f) - gap, height * 0.125f);
        _binRadius = _trayRadius / 1.42f;
        _pieceRadius = _binRadius * 0.9f;

        var binY = height * 0.30f;

        _binCenters = new SKPoint[count];
        for (var i = 0; i < count; i++)
        {
            _binCenters[i] = new SKPoint(margin + (slot * i) + (slot / 2f), binY);
        }

        _pieceHome = new SKPoint(width * 0.5f, height * 0.76f);
    }

    private void DrawBackground(SKCanvas canvas, float width, float height)
    {
        _background.Shader?.Dispose();
        _background.Shader = SKShader.CreateLinearGradient(
            new SKPoint(0, 0),
            new SKPoint(0, height),
            [PloofyPalette.SkyTop, PloofyPalette.SkyMiddle, PloofyPalette.SkyBottom],
            [0f, 0.5f, 1f],
            SKShaderTileMode.Clamp);
        canvas.DrawRect(0, 0, width, height, _background);
    }

    private void DrawBins(SKCanvas canvas, ShapeSortRound round)
    {
        for (var i = 0; i < round.Bins.Count; i++)
        {
            var center = _binCenters[i];
            var isHovered = _hoveredBin == i;

            // Kutunun altındaki açık tepsi: hayalet şekil boşlukta durmasın.
            _tray.Color = SKColors.White.WithAlpha(isHovered ? (byte)225 : (byte)150);
            canvas.DrawRoundRect(
                center.X - _trayRadius,
                center.Y - _trayRadius,
                _trayRadius * 2f,
                _trayRadius * 2f,
                _trayRadius * 0.30f,
                _trayRadius * 0.30f,
                _tray);

            _painter.DrawGhost(
                canvas,
                center,
                _binRadius,
                round.Bins[i],
                PloofyPalette.Ink,
                isHovered);
        }
    }

    private void DrawPieces(SKCanvas canvas, ShapeSortRound round)
    {
        // Sıradaki parça arkada soluk duruyor: oyun akışı duraksamıyor.
        if (round.Next is { } next)
        {
            _painter.Draw(
                canvas,
                new SKPoint(_pieceHome.X, _pieceHome.Y + (_pieceRadius * 0.55f)),
                _pieceRadius * 0.72f,
                next.Kind,
                PloofyPalette.For(next.Hue),
                alpha: 90);
        }

        // Kutuya yerleşen parça
        if (_settlingPiece is { } settling)
        {
            var t = MathF.Min(1f, (_time - _settleStartedAt) / SettleDuration);
            var eased = t * t;
            var point = new SKPoint(
                _settleFrom.X + ((_settleTo.X - _settleFrom.X) * eased),
                _settleFrom.Y + ((_settleTo.Y - _settleFrom.Y) * eased));

            _painter.Draw(
                canvas,
                point,
                _pieceRadius * (1f - (0.55f * t)),
                settling.Kind,
                PloofyPalette.For(settling.Hue),
                alpha: (byte)(255 * (1f - t)));
            return;
        }

        if (round.Current is not { } current)
        {
            return;
        }

        var center = _pieceHome;
        var scale = 1f;

        if (_isDragging)
        {
            center = DragCenter();
            // Sürüklenen parça biraz büyüyor: "elimde" hissi.
            scale = 1.12f;
        }
        else if (_time - _returnStartedAt < ReturnDuration)
        {
            var t = (_time - _returnStartedAt) / ReturnDuration;
            // Yaylanarak yerine oturuyor.
            var eased = 1f - MathF.Pow(1f - t, 3f);
            center = new SKPoint(
                _returnFrom.X + ((_pieceHome.X - _returnFrom.X) * eased),
                _returnFrom.Y + ((_pieceHome.Y - _returnFrom.Y) * eased));
        }
        else
        {
            // Bekleyen parça hafifçe nefes alıyor — ekranda durup kalmasın.
            center.Y += MathF.Sin(_time * 2.2f) * _pieceRadius * 0.05f;
        }

        var shakeAge = _time - _shakeStartedAt;
        if (shakeAge < ShakeDuration)
        {
            var damping = 1f - (shakeAge / ShakeDuration);
            center.X += MathF.Sin(shakeAge * 44f) * _pieceRadius * 0.3f * damping;
        }

        _painter.Draw(canvas, center, _pieceRadius, current.Kind, PloofyPalette.For(current.Hue), scale);
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
        _background.Shader?.Dispose();
        _background.Dispose();
        _tray.Dispose();
    }
}
