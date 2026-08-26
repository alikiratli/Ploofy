using Ploofy.Engine.Games;
using Ploofy.Ui.Painting;
using SkiaSharp;
using SkiaSharp.Views.Maui;
using SkiaSharp.Views.Maui.Controls;

namespace Ploofy.Ui.Controls;

/// <summary>Bir parçayı yuvaya bırakmanın ekrandaki karşılığı.</summary>
public sealed class JigsawDropEventArgs(PlaceOutcome outcome) : EventArgs
{
    public PlaceOutcome Outcome { get; } = outcome;
}

/// <summary>
/// Yapbozun çizim ve sürükleme yüzeyi.
/// </summary>
/// <remarks>
/// <para>
/// Resim bir varlık dosyası değil: <see cref="JigsawRound.PictureSeed"/>
/// tohumundan burada üretiliyor ve bir kez bitmap'e çiziliyor. Sebebi
/// pratik — uygulama hiç görsel varlık taşımıyor ve her yapboz için resim
/// çizdirmek, üç dilde adlandırmak ve her ekran yoğunluğu için ölçeklemek
/// demekti. Şekiller her hücreye düşecek biçimde dağıtılıyor: bir parça
/// düz zeminden ibaret kalırsa hayaletsiz bantta o parçanın yeri
/// bulunamaz hâle geliyor.
/// </para>
/// <para>
/// Parça yolları motordaki tırnak sayılarından türüyor; komşu kenarlar
/// birbirinin tersi olduğu için parça ile yuvası birebir oturuyor.
/// Sürükleme Şekil Ayırma'daki kalıbın aynısı: MAUI'nin sürükle-bırak
/// tanıyıcıları değil doğrudan dokunma olayları.
/// </para>
/// </remarks>
public sealed class JigsawSurface : SKCanvasView
{
    private const float ReturnDuration = 0.28f;
    private const float SettleDuration = 0.24f;
    private const float ShakeDuration = 0.4f;

    /// <summary>Tırnak yayının kaç doğru parçasıyla çizildiği.</summary>
    private const int KnobSegments = 20;

    /// <summary>Tırnağın kenardan dışarı taşma oranı ve yarıçapı.</summary>
    private const float KnobOffset = 0.13f;
    private const float KnobRadius = 0.16f;

    private readonly ParticleField _particles = new() { Gravity = 640f };
    private readonly Random _rng = new();

    private readonly SKPaint _background = new() { IsAntialias = true };
    private readonly SKPaint _board = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _image = new() { IsAntialias = true };
    private readonly SKPaint _outline = new()
    {
        IsAntialias = true,
        Style = SKPaintStyle.Stroke,
        StrokeJoin = SKStrokeJoin.Round,
    };

    private readonly Dictionary<int, SKPath> _paths = [];

    private SKImage? _picture;
    private int _pictureSeed;
    private int _pictureSize;

    private IDispatcherTimer? _ticker;
    private DateTime _lastFrame;
    private float _time;

    // Sürükleme durumu
    private bool _isDragging;
    private SKPoint _dragPoint;
    private SKPoint _grabOffset;
    private int _hoveredRow = -1;
    private int _hoveredColumn = -1;

    // Animasyon durumu
    private SKPoint _returnFrom;
    private float _returnStartedAt = float.MinValue;
    private SKPoint _settleFrom;
    private float _settleStartedAt = float.MinValue;
    private JigsawPiece? _settlingPiece;
    private float _shakeStartedAt = float.MinValue;

    // Son çizimde hesaplanan yerleşim.
    private float _originX;
    private float _originY;
    private float _side;
    private float _cell;
    private SKPoint _trayCenter;
    private float _trayScale = 1f;

    public JigsawSurface()
    {
        EnableTouchEvents = true;
        IgnorePixelScaling = false;
        PaintSurface += OnPaintSurface;
        Touch += OnTouch;
    }

    public JigsawRound? Round { get; private set; }

    /// <summary>Bir parça yuvaya bırakıldı (doğru ya da yanlış).</summary>
    public event EventHandler<JigsawDropEventArgs>? Dropped;

    /// <summary>Bütün parçalar yerleşti.</summary>
    public event EventHandler? RoundOver;

    public void Start(JigsawRound round)
    {
        Round = round;
        _time = 0f;
        _isDragging = false;
        _hoveredRow = -1;
        _hoveredColumn = -1;
        _settlingPiece = null;
        _returnStartedAt = float.MinValue;
        _settleStartedAt = float.MinValue;
        _shakeStartedAt = float.MinValue;
        _particles.Clear();
        ClearPaths();

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

        if (Round is not { } round || round.IsComplete || _settlingPiece is not null || _cell <= 0f)
        {
            return;
        }

        switch (e.ActionType)
        {
            case SKTouchAction.Pressed:
                // Tepsideki parçanın yakınına dokunmak da tutmaya yetiyor.
                var reach = _cell * _trayScale * 0.72f;
                var dx = e.Location.X - _trayCenter.X;
                var dy = e.Location.Y - _trayCenter.Y;

                if ((dx * dx) + (dy * dy) <= reach * reach)
                {
                    _isDragging = true;
                    _dragPoint = e.Location;
                    // Parça parmağın altına sıçramasın: tutulan nokta korunuyor.
                    _grabOffset = new SKPoint(
                        _trayCenter.X - e.Location.X,
                        _trayCenter.Y - e.Location.Y);
                    _returnStartedAt = float.MinValue;
                }

                break;

            case SKTouchAction.Moved when _isDragging:
                _dragPoint = e.Location;
                (_hoveredRow, _hoveredColumn) = SlotAt(DragCenter(), round);
                break;

            case SKTouchAction.Released or SKTouchAction.Cancelled when _isDragging:
                ReleaseAt(DragCenter(), round);
                break;
        }

        InvalidateSurface();
    }

    private SKPoint DragCenter() =>
        new(_dragPoint.X + _grabOffset.X, _dragPoint.Y + _grabOffset.Y);

    private void ReleaseAt(SKPoint center, JigsawRound round)
    {
        _isDragging = false;
        _hoveredRow = -1;
        _hoveredColumn = -1;

        var (row, column) = SlotAt(center, round);
        if (row < 0)
        {
            // Boşluğa bırakıldı: sessizce tepsiye dönüyor, hata sayılmıyor.
            StartReturn(center);
            return;
        }

        var piece = round.Current;
        var outcome = round.Place(row, column);

        switch (outcome)
        {
            case PlaceOutcome.Fitted when piece is not null:
                _settlingPiece = piece;
                _settleFrom = center;
                _settleStartedAt = _time;

                _particles.Burst(
                    HomeCenter(piece),
                    _cell * 0.35f,
                    PloofyPalette.Lime,
                    _rng,
                    count: 12);
                break;

            case PlaceOutcome.WrongSlot:
                _shakeStartedAt = _time;
                StartReturn(center);
                break;

            default:
                StartReturn(center);
                break;
        }

        Dropped?.Invoke(this, new JigsawDropEventArgs(outcome));
    }

    private void StartReturn(SKPoint from)
    {
        _returnFrom = from;
        _returnStartedAt = _time;
    }

    /// <summary>
    /// Verilen noktanın düştüğü boş yuva.
    /// </summary>
    /// <remarks>
    /// En yakın <b>boş</b> yuva aranıyor; dolu yuvalar hesaba katılmıyor,
    /// çünkü oraya bırakmak zaten anlamsız ve parçanın dolu bir yuvaya
    /// çekilmesi çocuğa "yanlış" demenin en kafa karıştırıcı yolu olurdu.
    /// </remarks>
    private (int Row, int Column) SlotAt(SKPoint point, JigsawRound round)
    {
        var reach = _cell * round.SnapReach * 0.5f;
        var best = reach * reach;
        var bestRow = -1;
        var bestColumn = -1;

        foreach (var piece in round.Pieces)
        {
            if (piece.IsPlaced)
            {
                continue;
            }

            var center = HomeCenter(piece);
            var dx = point.X - center.X;
            var dy = point.Y - center.Y;
            var distance = (dx * dx) + (dy * dy);

            if (distance <= best)
            {
                best = distance;
                bestRow = piece.Row;
                bestColumn = piece.Column;
            }
        }

        return (bestRow, bestColumn);
    }

    private SKPoint HomeCenter(JigsawPiece piece) => new(
        _originX + ((piece.Column + 0.5f) * _cell),
        _originY + ((piece.Row + 0.5f) * _cell));

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
        EnsurePicture(round);

        DrawBoard(canvas);
        DrawSlots(canvas, round);
        DrawPlaced(canvas, round);
        DrawTray(canvas, round);

        _particles.Draw(canvas);
    }

    /// <summary>
    /// Tahtayı ve tepsiyi ekrana oturtur.
    /// </summary>
    /// <remarks>
    /// Tahta kare: yapboz da kare ve dikdörtgen bir tahtada parçalar
    /// yamulurdu. Altta ayrı bir tepsi şeridi var — parça tahtanın üstünde
    /// beklerse hangi yuvanın boş olduğu okunmuyor.
    /// </remarks>
    private void Layout(JigsawRound round, float width, float height)
    {
        var top = height * 0.11f;
        var trayHeight = height * 0.21f;

        var side = MathF.Min(width * 0.92f, height - top - trayHeight);
        var originX = (width - side) / 2f;
        var originY = top + ((height - top - trayHeight - side) / 2f);

        // Yollar tahtanın yerine ve hücre boyutuna bağlı; geometri oynadıysa
        // önbellek geçersiz. Pencere yeniden boyutlandığında parçalar eski
        // yerlerinde kalıyordu.
        if (side != _side || originX != _originX || originY != _originY)
        {
            ClearPaths();
        }

        _side = side;
        _originX = originX;
        _originY = originY;
        _cell = _side / round.Grid;

        _trayCenter = new SKPoint(width * 0.5f, height - (trayHeight * 0.5f));

        // Tepsideki parça küçültülerek gösteriliyor, tutulunca tam boyuna
        // dönüyor: "elime aldım" hissi. Ölçek hücreye değil parçanın
        // <b>tırnaklarıyla birlikte</b> kapladığı yere göre hesaplanıyor —
        // hücreye göre hesaplandığında parça tepsiden taşıyordu.
        _trayScale = MathF.Min(1f, trayHeight * 0.62f / (_cell * 1.3f));
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

    /// <summary>Tahtanın altlığı: yuvaların nerede bittiği belli olsun.</summary>
    private void DrawBoard(SKCanvas canvas)
    {
        var pad = _cell * 0.14f;

        _board.Color = PloofyPalette.Ink.WithAlpha(28);
        canvas.DrawRoundRect(
            _originX - pad,
            _originY - pad,
            _side + (2f * pad),
            _side + (2f * pad),
            pad,
            pad,
            _board);
    }

    private void DrawSlots(SKCanvas canvas, JigsawRound round)
    {
        foreach (var piece in round.Pieces)
        {
            if (piece.IsPlaced && !ReferenceEquals(piece, _settlingPiece))
            {
                continue;
            }

            var path = PathFor(piece);
            var isHovered = piece.Row == _hoveredRow && piece.Column == _hoveredColumn;

            _board.Color = SKColors.White.WithAlpha(isHovered ? (byte)210 : (byte)130);
            canvas.DrawPath(path, _board);

            // Hayalet: resmin soluk kopyası. Yalnızca küçük bantlarda —
            // Meşe'de parçanın yeri komşulara bakarak bulunuyor.
            if (round.ShowsGhost && _picture is { } picture)
            {
                canvas.Save();
                canvas.ClipPath(path, antialias: true);
                _image.Color = SKColors.White.WithAlpha(isHovered ? (byte)120 : (byte)72);
                canvas.DrawImage(picture, BoardRect(), _image);
                canvas.Restore();
            }

            _outline.Color = PloofyPalette.Ink.WithAlpha(isHovered ? (byte)150 : (byte)70);
            _outline.StrokeWidth = MathF.Max(1.5f, _cell * (isHovered ? 0.035f : 0.02f));
            canvas.DrawPath(path, _outline);
        }
    }

    private void DrawPlaced(SKCanvas canvas, JigsawRound round)
    {
        if (_picture is not { } picture)
        {
            return;
        }

        foreach (var piece in round.Pieces)
        {
            if (!piece.IsPlaced || ReferenceEquals(piece, _settlingPiece))
            {
                continue;
            }

            var path = PathFor(piece);

            canvas.Save();
            canvas.ClipPath(path, antialias: true);
            _image.Color = SKColors.White;
            canvas.DrawImage(picture, BoardRect(), _image);
            canvas.Restore();

            // İnce kenar: yerleşen parçalar tek bir resme erimesin, çocuk
            // neyi koyduğunu görsün.
            _outline.Color = SKColors.White.WithAlpha(120);
            _outline.StrokeWidth = MathF.Max(1f, _cell * 0.018f);
            canvas.DrawPath(path, _outline);
        }
    }

    private void DrawTray(SKCanvas canvas, JigsawRound round)
    {
        // Yerine oturan parça
        if (_settlingPiece is { } settling)
        {
            var t = MathF.Min(1f, (_time - _settleStartedAt) / SettleDuration);
            var eased = 1f - MathF.Pow(1f - t, 3f);
            var home = HomeCenter(settling);

            DrawPiece(
                canvas,
                settling,
                new SKPoint(
                    _settleFrom.X + ((home.X - _settleFrom.X) * eased),
                    _settleFrom.Y + ((home.Y - _settleFrom.Y) * eased)),
                1f);
            return;
        }

        // Şekil Ayırma'daki gibi bir "sıradaki parça" önizlemesi yok. Orada
        // parçalar küçük ve arkada soluk duran ikincisi okunuyor; burada
        // parça tepsinin tamamını kaplıyor ve arkadaki yalnızca tırnağıyla
        // dışarı taşıyor — cihazda çizim hatası gibi görünüyordu.
        if (round.Current is not { } current)
        {
            return;
        }

        var center = _trayCenter;
        var scale = _trayScale;

        if (_isDragging)
        {
            center = DragCenter();
            // Sürüklenirken tam boyuna dönüyor: yuvayla aynı ölçekte olmadan
            // nereye oturacağı kestirilemiyor.
            scale = 1f;
        }
        else if (_time - _returnStartedAt < ReturnDuration)
        {
            var t = (_time - _returnStartedAt) / ReturnDuration;
            var eased = 1f - MathF.Pow(1f - t, 3f);
            center = new SKPoint(
                _returnFrom.X + ((_trayCenter.X - _returnFrom.X) * eased),
                _returnFrom.Y + ((_trayCenter.Y - _returnFrom.Y) * eased));
            scale = _trayScale + ((1f - _trayScale) * (1f - eased));
        }
        else
        {
            // Bekleyen parça hafifçe nefes alıyor — ekranda durup kalmasın.
            center.Y += MathF.Sin(_time * 2.2f) * _cell * 0.02f;
        }

        var shakeAge = _time - _shakeStartedAt;
        if (shakeAge < ShakeDuration)
        {
            var damping = 1f - (shakeAge / ShakeDuration);
            center.X += MathF.Sin(shakeAge * 42f) * _cell * 0.16f * damping;
        }

        DrawPiece(canvas, current, center, scale);
    }

    /// <summary>
    /// Parçayı verilen noktada çizer.
    /// </summary>
    /// <remarks>
    /// Parçanın yolu her zaman <b>kendi yuvasının</b> yerinde duruyor; parça
    /// başka bir yerde göründüğünde tuval kaydırılıyor ve resim onunla
    /// birlikte kayıyor. Böylece parçanın taşıdığı resim parçası her zaman
    /// yolun altına denk geliyor.
    /// </remarks>
    private void DrawPiece(
        SKCanvas canvas, JigsawPiece piece, SKPoint center, float scale, byte alpha = 255)
    {
        if (_picture is not { } picture || scale <= 0f)
        {
            return;
        }

        var home = HomeCenter(piece);
        var path = PathFor(piece);

        canvas.Save();
        canvas.Translate(center.X, center.Y);
        canvas.Scale(scale);
        canvas.Translate(-home.X, -home.Y);

        canvas.ClipPath(path, antialias: true);
        _image.Color = SKColors.White.WithAlpha(alpha);
        canvas.DrawImage(picture, BoardRect(), _image);

        canvas.Restore();

        // Kenar çizgisi kırpmanın dışında: kırpılınca yarısı kayboluyor.
        canvas.Save();
        canvas.Translate(center.X, center.Y);
        canvas.Scale(scale);
        canvas.Translate(-home.X, -home.Y);

        _outline.Color = SKColors.White.WithAlpha((byte)(alpha * 0.85f));
        _outline.StrokeWidth = MathF.Max(1.5f, _cell * 0.028f);
        canvas.DrawPath(path, _outline);

        canvas.Restore();
    }

    private SKRect BoardRect() => SKRect.Create(_originX, _originY, _side, _side);

    // --- Kesim ---

    private SKPath PathFor(JigsawPiece piece)
    {
        if (_paths.TryGetValue(piece.Id, out var cached))
        {
            return cached;
        }

        var left = _originX + (piece.Column * _cell);
        var top = _originY + (piece.Row * _cell);
        var right = left + _cell;
        var bottom = top + _cell;

        var path = new SKPath();
        path.MoveTo(left, top);

        // Saat yönünde dolaşılıyor: bu yönde bir kenarın dış normali
        // (dy, -dx) oluyor ve tırnak +1'de her zaman dışarı bakıyor.
        AddEdge(path, new SKPoint(left, top), new SKPoint(right, top), piece.Top);
        AddEdge(path, new SKPoint(right, top), new SKPoint(right, bottom), piece.Right);
        AddEdge(path, new SKPoint(right, bottom), new SKPoint(left, bottom), piece.Bottom);
        AddEdge(path, new SKPoint(left, bottom), new SKPoint(left, top), piece.Left);
        path.Close();

        _paths[piece.Id] = path;
        return path;
    }

    /// <summary>
    /// Bir kenarı çizer: düz ya da tırnaklı.
    /// </summary>
    /// <remarks>
    /// Tırnak, kenarın ortasına oturan bir dairenin <b>büyük</b> yayı. Daire
    /// kenarı iki noktada kestiği için yay tam o iki boyundan başlayıp
    /// bitiyor — ek bir birleştirme çizgisi gerekmiyor ve komşu parçanın
    /// ters işaretli tırnağı bu boşluğa birebir oturuyor.
    /// </remarks>
    private static void AddEdge(SKPath path, SKPoint from, SKPoint to, int tab)
    {
        if (tab == 0)
        {
            path.LineTo(to);
            return;
        }

        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var length = MathF.Sqrt((dx * dx) + (dy * dy));

        var ux = dx / length;
        var uy = dy / length;

        // Saat yönü dolaşımında dış normal.
        var nx = uy;
        var ny = -ux;

        var radius = KnobRadius * length;
        var offset = KnobOffset * length;
        var half = MathF.Sqrt((radius * radius) - (offset * offset));

        var midX = from.X + (ux * length * 0.5f);
        var midY = from.Y + (uy * length * 0.5f);

        var centerX = midX + (nx * offset * tab);
        var centerY = midY + (ny * offset * tab);

        var neck1 = new SKPoint(midX - (ux * half), midY - (uy * half));
        var neck2 = new SKPoint(midX + (ux * half), midY + (uy * half));

        path.LineTo(neck1);

        var a0 = MathF.Atan2(neck1.Y - centerY, neck1.X - centerX);
        var a1 = MathF.Atan2(neck2.Y - centerY, neck2.X - centerX);
        var far = MathF.Atan2(ny * tab, nx * tab);

        // Tırnağın ucundan geçen yönde dönülüyor; diğer yön kenarı kesip
        // geçen küçük yay olurdu.
        var sweep = a1 - a0;
        while (sweep <= 0f)
        {
            sweep += MathF.Tau;
        }

        var relative = far - a0;
        while (relative < 0f)
        {
            relative += MathF.Tau;
        }

        if (relative > sweep)
        {
            sweep -= MathF.Tau;
        }

        for (var i = 1; i <= KnobSegments; i++)
        {
            var angle = a0 + (sweep * i / KnobSegments);
            path.LineTo(
                centerX + (MathF.Cos(angle) * radius),
                centerY + (MathF.Sin(angle) * radius));
        }

        path.LineTo(to);
    }

    private void ClearPaths()
    {
        foreach (var path in _paths.Values)
        {
            path.Dispose();
        }

        _paths.Clear();
    }

    // --- Resim ---

    private void EnsurePicture(JigsawRound round)
    {
        var size = (int)MathF.Round(_side);
        if (size <= 0)
        {
            return;
        }

        if (_picture is not null && _pictureSeed == round.PictureSeed && _pictureSize == size)
        {
            return;
        }

        // Ölçek değiştiyse yollar da geçersiz: hücre boyutuna bağlılar.
        ClearPaths();

        _picture?.Dispose();
        _picture = BuildPicture(round.PictureSeed, size);
        _pictureSeed = round.PictureSeed;
        _pictureSize = size;
    }

    /// <summary>
    /// Yapbozun resmini üretir.
    /// </summary>
    /// <remarks>
    /// Şekiller sarsılmış bir ızgaraya dağıtılıyor, rastgele serpilmiyor:
    /// serpme kaçınılmaz olarak boş bölgeler bırakıyor ve düz zeminden
    /// ibaret kalan bir parçanın yeri, hayaletin olmadığı bantta
    /// bulunamıyor.
    /// </remarks>
    private static SKImage BuildPicture(int seed, int size)
    {
        var rng = new Random(seed);
        var kinds = Enum.GetValues<ShapeKind>();
        var hues = PloofyPalette.All;

        using var surface = SKSurface.Create(new SKImageInfo(size, size));
        var canvas = surface.Canvas;

        var first = hues[rng.Next(hues.Count)];
        var second = hues[rng.Next(hues.Count)];

        using (var sky = new SKPaint { IsAntialias = true })
        {
            sky.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0, 0),
                new SKPoint(size, size),
                [first.Light, second.Light],
                [0f, 1f],
                SKShaderTileMode.Clamp);
            canvas.DrawRect(0, 0, size, size, sky);
            sky.Shader.Dispose();
        }

        using var painter = new ShapePainter();

        const int columns = 4;
        var step = size / (float)columns;

        for (var row = 0; row < columns; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                var jitterX = ((float)rng.NextDouble() - 0.5f) * step * 0.5f;
                var jitterY = ((float)rng.NextDouble() - 0.5f) * step * 0.5f;

                painter.Draw(
                    canvas,
                    new SKPoint(
                        ((column + 0.5f) * step) + jitterX,
                        ((row + 0.5f) * step) + jitterY),
                    step * (0.30f + ((float)rng.NextDouble() * 0.20f)),
                    kinds[rng.Next(kinds.Length)],
                    hues[rng.Next(hues.Count)]);
            }
        }

        return surface.Snapshot();
    }

    protected override void OnHandlerChanged()
    {
        base.OnHandlerChanged();

        if (Handler is not null)
        {
            return;
        }

        Stop();
        ClearPaths();
        _picture?.Dispose();
        _picture = null;
        _particles.Dispose();
        _background.Shader?.Dispose();
        _background.Dispose();
        _board.Dispose();
        _image.Dispose();
        _outline.Dispose();
    }
}
