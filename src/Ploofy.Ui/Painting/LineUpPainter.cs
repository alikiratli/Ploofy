using Ploofy.Engine.Games;
using SkiaSharp;

namespace Ploofy.Ui.Painting;

/// <summary>
/// Sırala'nın ekrandaki yerleşimi: yuva ve tepsi noktaları.
/// </summary>
/// <remarks>
/// Dokunma isabeti de çizim de aynı ölçülere bakıyor. Ayrı hesaplanan iki
/// yerleşim, parmağın gördüğü yerin bir yerine ışınlanması demek.
/// </remarks>
public sealed record LineUpLayout(
    IReadOnlyList<SKPoint> Slots,
    IReadOnlyList<SKPoint> Tray,
    float SlotHalf,
    float PieceRadius)
{
    /// <summary>
    /// Yerleşimi hesaplar.
    /// </summary>
    /// <param name="topInset">
    /// Üstteki bilgi şeridi için bırakılan pay. Yoksa ilk yuva şeridin altında
    /// kalıyor ve oraya parça bırakılamıyor.
    /// </param>
    public static LineUpLayout For(
        float width, float height, int slotCount, int trayCount, float topInset = 0.13f)
    {
        var top = height * topInset;
        var usable = height - top;

        var slotSpan = width * 0.86f;
        var slotPitch = slotSpan / Math.Max(1, slotCount);

        var slotHalf = MathF.Min(slotPitch * 0.42f, usable * 0.19f);

        var slotY = top + (usable * 0.30f);
        var trayY = top + (usable * 0.76f);
        var left = (width - slotSpan) / 2f;

        var slots = new SKPoint[slotCount];
        for (var i = 0; i < slotCount; i++)
        {
            slots[i] = new SKPoint(left + (slotPitch * (i + 0.5f)), slotY);
        }

        // Tepsi ortalanıyor ve parça eksildikçe kalanlar ortaya toplanıyor:
        // sabit yuvalara yaslı bir tepside boşluklar "burada bir şey vardı"
        // diye okunuyor.
        var trayPitch = MathF.Min(slotPitch, slotSpan / Math.Max(1, trayCount));
        var trayLeft = (width - (trayPitch * trayCount)) / 2f;

        var tray = new SKPoint[trayCount];
        for (var i = 0; i < trayCount; i++)
        {
            tray[i] = new SKPoint(trayLeft + (trayPitch * (i + 0.5f)), trayY);
        }

        return new LineUpLayout(slots, tray, slotHalf, slotHalf * 0.78f);
    }

    /// <summary>Noktanın üstünde durduğu yuva; yoksa -1.</summary>
    public int SlotAt(SKPoint point)
    {
        for (var i = 0; i < Slots.Count; i++)
        {
            if (MathF.Abs(point.X - Slots[i].X) <= SlotHalf &&
                MathF.Abs(point.Y - Slots[i].Y) <= SlotHalf)
            {
                return i;
            }
        }

        return -1;
    }
}

/// <summary>
/// Sırala'nın çizimi.
/// </summary>
/// <remarks>
/// <para>
/// MAUI'den bağımsız, yalnızca SkiaSharp: yerleşim ekrana bakmadan
/// doğrulanamıyor ve bu sınıf küçük bir konsol programından çağrılıp PNG'ye
/// çizilebiliyor. Rapordaki grafikte işe yarayan yol aynı.
/// </para>
/// <para>
/// Yuvaların hepsi <b>aynı boyutta</b>: yuvayı içine gireceğe göre çizmek
/// cevabı söylerdi.
/// </para>
/// </remarks>
public sealed class LineUpPainter : IDisposable
{
    private readonly ShapePainter _painter = new();

    private readonly SKPaint _background = new() { IsAntialias = true };
    private readonly SKPaint _card = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _slot = new() { IsAntialias = true, Style = SKPaintStyle.Stroke };

    /// <summary>Oyun göğünü çizer.</summary>
    public void DrawBackground(SKCanvas canvas, float width, float height)
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

    /// <summary>
    /// Yönü anlatan işaret: küçük daire — çizgi — büyük daire.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Okumayan bir çocuğa "küçükten büyüğe" demenin tek yolu bu ve üç dilde
    /// de aynı çalışıyor. Ters yönde daireler yer değiştiriyor.
    /// </para>
    /// <para>
    /// İki daireyi birbirine bağlayan çizgi şart: bağsız iki daire ekranın
    /// köşelerinde duran iki süs gibi okunuyordu, "şu sıra şöyle gidiyor"
    /// gibi değil.
    /// </para>
    /// <para>
    /// İşaret yuvaların <b>altında</b>, tepsiyle arasındaki boşlukta duruyor.
    /// Üstteki yerinde, sayfanın bilgi şeridinin hemen altına denk geliyordu
    /// ve dar bir ekranda şeridin arkasında kalıyordu.
    /// </para>
    /// </remarks>
    public void DrawDirectionHint(SKCanvas canvas, LineUpLayout layout, SortDirection direction)
    {
        if (layout.Slots.Count < 2)
        {
            return;
        }

        var y = layout.Slots[0].Y + layout.SlotHalf +
            ((layout.Tray[0].Y - layout.SlotHalf - layout.Slots[0].Y - layout.SlotHalf) / 2f);

        var small = layout.SlotHalf * 0.14f;
        var large = layout.SlotHalf * 0.30f;

        var ascending = direction == SortDirection.Ascending;

        var leftRadius = ascending ? small : large;
        var rightRadius = ascending ? large : small;

        var leftX = layout.Slots[0].X;
        var rightX = layout.Slots[^1].X;

        _slot.Color = PloofyPalette.Ink.WithAlpha(55);
        _slot.StrokeWidth = MathF.Max(2f, layout.SlotHalf * 0.045f);
        _slot.PathEffect?.Dispose();
        _slot.PathEffect = null;
        canvas.DrawLine(leftX + leftRadius, y, rightX - rightRadius, y, _slot);

        _card.Color = PloofyPalette.Ink.WithAlpha(95);
        canvas.DrawCircle(leftX, y, leftRadius, _card);
        canvas.DrawCircle(rightX, y, rightRadius, _card);
    }

    /// <summary>Yuvaları çizer; dolu olanlar parçasıyla birlikte.</summary>
    public void DrawSlots(
        SKCanvas canvas, LineUpLayout layout, LineUpRound round, int hoveredSlot)
    {
        for (var i = 0; i < layout.Slots.Count && i < round.Slots.Count; i++)
        {
            var center = layout.Slots[i];

            var half = layout.SlotHalf;

            if (round.Slots[i] is { } filled)
            {
                // Çerçeve dolu yuvada da duruyor, yalnızca soluklaşıyor.
                // Kaybolunca yerleşen parça yanındaki boş yuvadan küçük
                // görünüyor ve "küçüldü" diye okunuyordu.
                _slot.Color = PloofyPalette.Ink.WithAlpha(28);
                _slot.StrokeWidth = MathF.Max(2f, half * 0.05f);
                _slot.PathEffect?.Dispose();
                _slot.PathEffect = null;

                canvas.DrawRoundRect(
                    center.X - half, center.Y - half, half * 2f, half * 2f,
                    half * 0.28f, half * 0.28f, _slot);

                DrawPiece(canvas, layout, filled, center, round.Attribute);
                continue;
            }

            var highlighted = i == hoveredSlot;

            _slot.Color = PloofyPalette.Ink.WithAlpha(highlighted ? (byte)200 : (byte)70);
            _slot.StrokeWidth = MathF.Max(2f, half * (highlighted ? 0.10f : 0.07f));
            _slot.PathEffect?.Dispose();
            _slot.PathEffect = highlighted
                ? null
                : SKPathEffect.CreateDash([half * 0.30f, half * 0.20f], 0);

            canvas.DrawRoundRect(
                center.X - half, center.Y - half, half * 2f, half * 2f,
                half * 0.28f, half * 0.28f, _slot);

            _slot.PathEffect?.Dispose();
            _slot.PathEffect = null;
        }
    }

    /// <summary>
    /// Tepsideki parçaları çizer.
    /// </summary>
    /// <param name="skip">
    /// Çizilmeyecek parça (sürüklenen ya da yerine dönmekte olan). İki kopya
    /// aynı anda görünmesin diye.
    /// </param>
    public void DrawTray(
        SKCanvas canvas, LineUpLayout layout, LineUpRound round, LineUpPiece? skip = null)
    {
        for (var i = 0; i < round.Tray.Count && i < layout.Tray.Count; i++)
        {
            var piece = round.Tray[i];
            if (piece.Id == skip?.Id)
            {
                continue;
            }

            DrawPiece(canvas, layout, piece, layout.Tray[i], round.Attribute);
        }
    }

    /// <summary>
    /// Parçayı çizer.
    /// </summary>
    /// <remarks>
    /// Boyuta göre sıralamada parça çıplak: kart içine konsa bütün kartlar
    /// aynı boyutta olur ve "küçük" işareti zayıflar. Miktara göre sıralamada
    /// kart var, çünkü dağınık duran beş nesne bir arada tek bir şey olarak
    /// okunmuyor.
    /// </remarks>
    public void DrawPiece(
        SKCanvas canvas,
        LineUpLayout layout,
        LineUpPiece piece,
        SKPoint center,
        SortAttribute attribute,
        float scale = 1f)
    {
        var hue = PloofyPalette.For(piece.Hue);

        if (attribute == SortAttribute.Size)
        {
            _painter.Draw(
                canvas, center, layout.PieceRadius * piece.Size * scale, piece.Kind, hue);
            return;
        }

        var half = layout.PieceRadius * scale;

        _card.Color = SKColors.White.WithAlpha(210);
        canvas.DrawRoundRect(
            center.X - half, center.Y - half, half * 2f, half * 2f,
            half * 0.26f, half * 0.26f, _card);

        DrawCluster(canvas, piece, center, half, hue);
    }

    /// <summary>
    /// Kartın içindeki nesne kümesi.
    /// </summary>
    /// <remarks>
    /// Satırlar ortalanıyor: eksik kalan son satır sola yaslı çizilince küme
    /// sağa kaymış görünüyor ve iki kartı gözle kıyaslamak zorlaşıyor.
    /// Hücre ölçüsü karenin <b>içine</b> göre: on iki nesne de kartın dışına
    /// taşmadan sığmak zorunda.
    /// </remarks>
    private void DrawCluster(
        SKCanvas canvas, LineUpPiece piece, SKPoint center, float half, HuePaint hue)
    {
        var columns = (int)MathF.Ceiling(MathF.Sqrt(piece.Count));
        var rows = (int)MathF.Ceiling(piece.Count / (float)columns);

        var cell = (half * 1.72f) / Math.Max(columns, rows);
        var radius = cell * 0.38f;

        for (var i = 0; i < piece.Count; i++)
        {
            var row = i / columns;
            var column = i % columns;
            var inRow = MathF.Min(columns, piece.Count - (row * columns));

            var x = center.X + ((column - ((inRow - 1) / 2f)) * cell);
            var y = center.Y + ((row - ((rows - 1) / 2f)) * cell);

            _painter.Draw(canvas, new SKPoint(x, y), radius, piece.Kind, hue);
        }
    }

    public void Dispose()
    {
        _painter.Dispose();
        _background.Shader?.Dispose();
        _slot.PathEffect?.Dispose();
        _background.Dispose();
        _card.Dispose();
        _slot.Dispose();
    }
}
