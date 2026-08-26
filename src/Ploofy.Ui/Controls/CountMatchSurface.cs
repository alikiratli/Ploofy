using System.Globalization;
using Ploofy.Engine.Games;
using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>Kümenin bir rakama bırakılmasının ekrandaki karşılığı.</summary>
public sealed class CountDropEventArgs(CountOutcome outcome, int digit) : EventArgs
{
    public CountOutcome Outcome { get; } = outcome;

    public int Digit { get; } = digit;
}

/// <summary>
/// Say ve Eşleştir'in çizim ve sürükleme yüzeyi.
/// </summary>
/// <remarks>
/// <para>
/// Şekil Ayırma ile aynı sürükleme kalıbı: MAUI'nin sürükle-bırak
/// tanıyıcıları değil doğrudan dokunma olayları, çünkü platformun sürükleme
/// eşiği küçük çocuğun yavaş hareketinde aşılmıyor ve parça hiç kıpırdamıyor.
/// </para>
/// <para>
/// Sürüklenen şey tek bir nesne değil <b>kümenin tamamı</b>. Nesneler bu
/// yüzden açık bir kartın üstünde duruyor: kart olmadan çocuk tek bir elmayı
/// tutmaya çalışıyor ve kümenin bir bütün olduğunu göremiyor.
/// </para>
/// </remarks>
public sealed class CountMatchSurface : SKCanvasView
{
    private const float ReturnDuration = 0.28f;
    private const float SettleDuration = 0.26f;
    private const float ShakeDuration = 0.4f;

    /// <summary>Kartın ızgara sütun sayısı — onluk çerçevenin yarısı.</summary>
    /// <remarks>
    /// Beşerli dizilim tesadüf değil: 7 nesne "bir tam sıra ve iki tane"
    /// olarak görünüyor ve çocuk tek tek saymadan da miktarı yakalayabiliyor.
    /// Aynı sebeple sütun sayısı kümeye göre değişmiyor — nesne boyutu sabit
    /// kalsın ki 3 ile 8 arasındaki fark boyuttan değil sayıdan okunsun.
    /// </remarks>
    private const int Columns = 5;

    private readonly ShapePainter _painter = new();
    private readonly ParticleField _particles = new() { Gravity = 700f };
    private readonly Random _rng = new();

    private readonly SKPaint _background = new() { IsAntialias = true };
    private readonly SKPaint _tray = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _digit = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKFont _digitFont = new(SKTypeface.Default);

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
    private float _shakeStartedAt = float.MinValue;

    // Yerleşme animasyonu boyunca ekranda kalan soru. Motor doğru cevapta
    // sıradaki soruya geçiyor, ama rakamlar kartın altında değişirse çocuk
    // kümenin hangi rakama girdiğini göremiyor — ve öğrendiği an tam orası.
    private CountQuestion? _settlingQuestion;
    private int _settlingBin = -1;

    // Dağınık yerleşimin sarsıntısı. Soru başına bir kez üretiliyor: her
    // karede yeniden çekilse nesneler ekranda titrer ve sayılamaz hâle gelir.
    private CountQuestion? _laidOutQuestion;
    private SKPoint[] _itemJitter = [];

    // Son çizimde hesaplanan yerleşim; dokunma isabeti bunu kullanıyor.
    private SKPoint[] _binCenters = [];
    private float _binRadius;
    private SKPoint _cardHome;
    private SKSize _cardSize;

    public CountMatchSurface()
    {
        EnableTouchEvents = true;
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
        Touch += OnTouch;
    }

    public CountMatchRound? Round { get; private set; }

    /// <summary>Küme bir rakama bırakıldı (doğru ya da yanlış).</summary>
    public event EventHandler<CountDropEventArgs>? Dropped;

    /// <summary>Bütün sorular doğru cevaplandı.</summary>
    public event EventHandler? RoundOver;

    public void Start(CountMatchRound round)
    {
        Round = round;
        _time = 0f;
        _isDragging = false;
        _hoveredBin = -1;
        _settlingQuestion = null;
        _settlingBin = -1;
        _laidOutQuestion = null;
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

            if (_settlingQuestion is not null && _time - _settleStartedAt >= SettleDuration)
            {
                _settlingQuestion = null;
                _settlingBin = -1;

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

        if (Round is not { } round || round.IsComplete || _settlingQuestion is not null)
        {
            return;
        }

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed when IsOnCard(e.Location):
                _isDragging = true;
                _dragPoint = e.Location;
                // Kart parmağın altına sıçramasın: tutulan nokta korunuyor.
                _grabOffset = new SKPoint(_cardHome.X - e.Location.X, _cardHome.Y - e.Location.Y);
                _returnStartedAt = float.MinValue;
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

    /// <summary>
    /// Kartın tutma alanı. Çizilenden geniş: kartın kenarına değen parmak da
    /// tutmaya yetmeli.
    /// </summary>
    private bool IsOnCard(SKPoint point) =>
        MathF.Abs(point.X - _cardHome.X) <= (_cardSize.Width * 0.60f) &&
        MathF.Abs(point.Y - _cardHome.Y) <= (_cardSize.Height * 0.60f);

    private SKPoint DragCenter() =>
        new(_dragPoint.X + _grabOffset.X, _dragPoint.Y + _grabOffset.Y);

    private void ReleaseAt(SKPoint center, CountMatchRound round)
    {
        _isDragging = false;
        _hoveredBin = -1;

        var binIndex = BinAt(center);
        if (binIndex < 0 || round.Current is not { } question)
        {
            // Boşluğa bırakıldı: sessizce yerine dönüyor, hata sayılmıyor.
            StartReturn(center);
            return;
        }

        var group = question.Group;
        var digit = question.Choices[binIndex];
        var outcome = round.Drop(digit);

        switch (outcome)
        {
            case CountOutcome.Correct:
                _settlingQuestion = question;
                _settlingBin = binIndex;
                _settleFrom = center;
                _settleTo = _binCenters[binIndex];
                _settleStartedAt = _time;

                _particles.Burst(
                    _binCenters[binIndex],
                    _binRadius * 0.9f,
                    PloofyPalette.For(group.Hue),
                    _rng,
                    count: 14);
                break;

            case CountOutcome.Wrong:
                _shakeStartedAt = _time;
                StartReturn(center);
                break;

            default:
                StartReturn(center);
                break;
        }

        Dropped?.Invoke(this, new CountDropEventArgs(outcome, digit));
    }

    private void StartReturn(SKPoint from)
    {
        _returnFrom = from;
        _returnStartedAt = _time;
    }

    private int BinAt(SKPoint point)
    {
        // İsabet alanı çizilenden geniş: kartı rakamın tam ortasına bırakmak
        // bu yaşta beklenemez, yakınına bırakmak yeterli olmalı.
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
        DrawDigits(canvas, round);
        DrawCard(canvas, round);

        _particles.Draw(canvas);
    }

    private void Layout(CountMatchRound round, float width, float height)
    {
        var count = round.Current?.Choices.Count ?? _binCenters.Length;
        if (count <= 0)
        {
            return;
        }

        // Tepsi boyutu önce, rakam ondan türetiliyor — Şekil Ayırma'daki
        // sıranın aynısı; tersi yapıldığında kenardaki tepsiler ekrandan taşıyor.
        var margin = width * 0.025f;
        var gap = width * 0.014f;
        var slot = (width - (2f * margin)) / count;

        _binRadius = MathF.Min((slot / 2f) - gap, height * 0.115f);
        _digitFont.Size = _binRadius * 1.5f;

        var binY = height * 0.30f;

        _binCenters = new SKPoint[count];
        for (var i = 0; i < count; i++)
        {
            _binCenters[i] = new SKPoint(margin + (slot * i) + (slot / 2f), binY);
        }

        // Kart, kümenin kaç sıra tuttuğuna göre yükseliyor. Sabit yükseklikte
        // tek sıralık bir küme kartın ortasında asılı kalıyor ve altında
        // yarım ekranlık boş bir alan duruyordu — cihazda görüldü.
        var items = round.Current?.Group.Count ?? Columns;
        var rows = (int)MathF.Ceiling(items / (float)Columns);
        var cardWidth = width * 0.72f;
        var cell = cardWidth / Columns;

        _cardSize = new SKSize(cardWidth, MathF.Min(height * 0.34f, cell * rows * 1.3f));

        // Kartın altı her zaman aynı yerde: sıra sayısı değiştikçe kart
        // yukarı doğru büyüyor, aşağı doğru kaymıyor.
        _cardHome = new SKPoint(width * 0.5f, (height * 0.88f) - (_cardSize.Height / 2f));
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

    private void DrawDigits(SKCanvas canvas, CountMatchRound round)
    {
        // Yerleşme sürerken ekranda hâlâ cevaplanan soru duruyor.
        if ((_settlingQuestion ?? round.Current) is not { } question)
        {
            return;
        }

        var metrics = _digitFont.Metrics;

        for (var i = 0; i < question.Choices.Count && i < _binCenters.Length; i++)
        {
            var center = _binCenters[i];
            var isHovered = _hoveredBin == i || _settlingBin == i;

            _tray.Color = SKColors.White.WithAlpha(isHovered ? (byte)245 : (byte)165);
            canvas.DrawRoundRect(
                center.X - _binRadius,
                center.Y - _binRadius,
                _binRadius * 2f,
                _binRadius * 2f,
                _binRadius * 0.32f,
                _binRadius * 0.32f,
                _tray);

            // Rakam dikeyde gerçekten ortalanıyor: taban çizgisine göre
            // çizmek 1 ile 8'i ekranda farklı yüksekliklere düşürüyor.
            var baseline = center.Y - ((metrics.Ascent + metrics.Descent) / 2f);

            _digit.Color = PloofyPalette.Ink.WithAlpha(isHovered ? (byte)255 : (byte)205);
            canvas.DrawText(
                question.Choices[i].ToString(CultureInfo.InvariantCulture),
                center.X,
                baseline,
                SKTextAlign.Center,
                _digitFont,
                _digit);
        }
    }

    private void DrawCard(SKCanvas canvas, CountMatchRound round)
    {
        // Rakama yerleşen küme: küçülerek rakamın içine giriyor.
        if (_settlingQuestion is { Group: var settling })
        {
            var t = MathF.Min(1f, (_time - _settleStartedAt) / SettleDuration);
            var eased = t * t;
            var point = new SKPoint(
                _settleFrom.X + ((_settleTo.X - _settleFrom.X) * eased),
                _settleFrom.Y + ((_settleTo.Y - _settleFrom.Y) * eased));

            DrawGroup(canvas, settling, point, 1f - (0.7f * t), (byte)(255 * (1f - t)));
            return;
        }

        if (round.Current is not { } question)
        {
            return;
        }

        EnsureJitter(question, round.ScattersItems);

        var center = _cardHome;
        var scale = 1f;

        if (_isDragging)
        {
            center = DragCenter();
            // Sürüklenen kart biraz büyüyor: "elimde" hissi.
            scale = 1.06f;
        }
        else if (_time - _returnStartedAt < ReturnDuration)
        {
            var t = (_time - _returnStartedAt) / ReturnDuration;
            var eased = 1f - MathF.Pow(1f - t, 3f);
            center = new SKPoint(
                _returnFrom.X + ((_cardHome.X - _returnFrom.X) * eased),
                _returnFrom.Y + ((_cardHome.Y - _returnFrom.Y) * eased));
        }
        else
        {
            // Bekleyen kart hafifçe nefes alıyor — ekranda durup kalmasın.
            center.Y += MathF.Sin(_time * 2.0f) * _cardSize.Height * 0.012f;
        }

        var shakeAge = _time - _shakeStartedAt;
        if (shakeAge < ShakeDuration)
        {
            var damping = 1f - (shakeAge / ShakeDuration);
            center.X += MathF.Sin(shakeAge * 40f) * _cardSize.Width * 0.05f * damping;
        }

        DrawGroup(canvas, question.Group, center, scale, alpha: 255);
    }

    /// <summary>Kümeyi kendi kartıyla birlikte çizer.</summary>
    private void DrawGroup(SKCanvas canvas, CountGroup group, SKPoint center, float scale, byte alpha)
    {
        if (scale <= 0f || alpha == 0)
        {
            return;
        }

        var width = _cardSize.Width * scale;
        var height = _cardSize.Height * scale;

        _tray.Color = SKColors.White.WithAlpha((byte)(alpha * 0.78f));
        canvas.DrawRoundRect(
            center.X - (width / 2f),
            center.Y - (height / 2f),
            width,
            height,
            height * 0.14f,
            height * 0.14f,
            _tray);

        var rows = (int)MathF.Ceiling(group.Count / (float)Columns);
        var cellWidth = width / Columns;
        var cellHeight = height / rows;
        var radius = MathF.Min(cellWidth, cellHeight) * 0.34f;
        var hue = PloofyPalette.For(group.Hue);

        for (var i = 0; i < group.Count; i++)
        {
            var row = i / Columns;
            var column = i % Columns;

            // Son sıra eksik kalabiliyor; ortalanıyor ki küme sağa yaslı
            // görünmesin.
            var inRow = MathF.Min(Columns, group.Count - (row * Columns));
            var x = center.X + ((column - ((inRow - 1) / 2f)) * cellWidth);
            var y = center.Y + ((row - ((rows - 1) / 2f)) * cellHeight);

            if (i < _itemJitter.Length)
            {
                x += _itemJitter[i].X * cellWidth * scale;
                y += _itemJitter[i].Y * cellHeight * scale;
            }

            _painter.Draw(canvas, new SKPoint(x, y), radius, group.Kind, hue, alpha: alpha);
        }
    }

    /// <summary>
    /// Dağınık yerleşimin sarsıntısını soru başına bir kez üretir.
    /// </summary>
    /// <remarks>
    /// Sarsıntı ızgara hücresinin dörtte biriyle sınırlı: nesneler üst üste
    /// binerse sayılamaz hâle geliyor ve oyun zor değil imkânsız oluyor.
    /// </remarks>
    private void EnsureJitter(CountQuestion question, bool scatters)
    {
        if (ReferenceEquals(_laidOutQuestion, question))
        {
            return;
        }

        _laidOutQuestion = question;

        if (!scatters)
        {
            _itemJitter = [];
            return;
        }

        _itemJitter = new SKPoint[question.Group.Count];
        for (var i = 0; i < _itemJitter.Length; i++)
        {
            _itemJitter[i] = new SKPoint(
                ((float)_rng.NextDouble() - 0.5f) * 0.5f,
                ((float)_rng.NextDouble() - 0.5f) * 0.5f);
        }
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
        _digit.Dispose();
        _digitFont.Dispose();
    }
}
