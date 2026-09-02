using Ploofy.Engine.Progress;
using SkiaSharp;

namespace Ploofy.Ui.Painting;

/// <summary>
/// Günlük oyun süresinin sütun grafiğini çizer.
/// </summary>
/// <remarks>
/// <para>
/// MAUI'den bağımsız, yalnızca SkiaSharp: grafiğin yerleşimi ekrana bakmadan
/// doğrulanamayan bir şey ve bu sınıf küçük bir konsol programından çağrılıp
/// PNG'ye çizilebiliyor. Yapbozun tırnak kesiminde işe yarayan yol aynı.
/// </para>
/// <para>
/// <b>Sütun, çizgi değil.</b> Günler ayrık: aradaki iki günü birleştiren bir
/// çizgi olmayan bir sürekliliği anlatıyor ve oynanmayan günü görünmez
/// kılıyor. Oynanmayan gün burada boş kalıyor — raporun asıl söylediklerinden
/// biri o.
/// </para>
/// <para>
/// Ölçek her zaman <b>sıfırdan</b> başlıyor. Tabanı kırpmak küçük farkları
/// büyük gösteriyor ve "dün üç katı oynadı" gibi olmayan bir şey okutuyor.
/// </para>
/// <para>
/// Tek ölçü, tek renk, gösterge yok: kartın başlığı zaten neyin çizildiğini
/// söylüyor ve tek kutucuklu bir gösterge başlığı tekrar edip yer yiyor.
/// </para>
/// </remarks>
public sealed class TrendPainter : IDisposable
{
    /// <summary>Sütunun en fazla kalınlığı (yoğunluk ölçeğiyle çarpılıyor).</summary>
    private const float MaxBarWidth = 24f;

    /// <summary>
    /// Sütunun yuvasında kaplayabileceği en fazla oran.
    /// </summary>
    /// <remarks>
    /// Yalnızca kalınlık sınırı yetmiyor: yedi günlük dönemde yuva genişleyince
    /// sütunlar yan yana yapışıp duvara dönüşüyordu. Kalan yer boşluk olarak
    /// duruyor ve günler ayrı ayrı okunuyor.
    /// </remarks>
    private const float MaxSlotFill = 0.55f;

    /// <summary>Sütunun üst ucunun yuvarlaklığı; tabanı köşeli kalıyor.</summary>
    private const float CapRadius = 4f;

    /// <summary>
    /// Oynanmış bir günün en kısa sütunu.
    /// </summary>
    /// <remarks>
    /// Bir dakika oynanan gün, hiç oynanmayan günle aynı görünmemeli: sıfır
    /// yükseklikli bir sütun "hiç oynamadı" diyor ve bu yanlış.
    /// </remarks>
    private const float MinPlayedHeight = 3f;

    /// <summary>Tasarımın ölçüldüğü yükseklik; yazı boyutları buna göre ölçekleniyor.</summary>
    private const float ReferenceHeight = 190f;

    /// <summary>Ocean'ın koyu ucu. Beyaz kart üstünde 3:1 kontrastı geçiyor.</summary>
    private static readonly SKColor BarColor = new(0x0F, 0x6F, 0xC4);

    private readonly SKPaint _bar = new() { IsAntialias = true, Style = SKPaintStyle.Fill };
    private readonly SKPaint _rule = new()
    {
        IsAntialias = false,
        Style = SKPaintStyle.Stroke,
        StrokeWidth = 1f,
    };

    private readonly SKPaint _text = new() { IsAntialias = true, Style = SKPaintStyle.Fill };

    /// <summary>
    /// Grafiği çizer.
    /// </summary>
    /// <param name="days">Günler, eskiden yeniye. Sonuncusu bugün sayılıyor.</param>
    /// <param name="weekdayInitials">
    /// Gün harfleri, <see cref="DayOfWeek"/> sırasıyla (Pazar'dan Cumartesi'ye).
    /// Arayüz katmanı üç dili tanımadığı için dışarıdan geliyor.
    /// </param>
    /// <param name="minuteSuffix">Tepe etiketinin birimi ("dk" / "min" / "Min.").</param>
    public void Draw(
        SKCanvas canvas,
        float width,
        float height,
        IReadOnlyList<ReportDay> days,
        IReadOnlyList<string> weekdayInitials,
        string minuteSuffix)
    {
        if (days.Count == 0 || width <= 0f || height <= 0f)
        {
            return;
        }

        var scale = height / ReferenceHeight;

        using var labelFont = new SKFont(SKTypeface.Default, 11f * scale);
        using var valueFont = new SKFont(SKTypeface.Default, 13f * scale) { Embolden = true };

        // Üstte tepe etiketi, altta gün harfleri için pay.
        var topPad = 24f * scale;
        var bottomPad = 22f * scale;

        var baseline = height - bottomPad;
        var plotHeight = baseline - topPad;

        if (plotHeight <= 0f)
        {
            return;
        }

        var peak = days.Max(d => d.Duration);
        var slot = width / days.Count;
        var barWidth = MathF.Max(2f, MathF.Min(MaxBarWidth * scale, slot * MaxSlotFill));

        DrawBaseline(canvas, width, baseline);

        // Hiç oynanmamışsa yalnızca taban çizgisi ve gün harfleri kalıyor:
        // boş bir grafik, uydurma bir ölçekten iyi.
        if (peak > TimeSpan.Zero)
        {
            DrawPeakRule(canvas, width, topPad, peak, minuteSuffix, valueFont, scale);
        }

        var today = days[^1].Date;
        var labelStep = LabelStep(weekdayInitials, labelFont, slot, scale);

        for (var i = 0; i < days.Count; i++)
        {
            var day = days[i];
            var centerX = (slot * i) + (slot / 2f);

            if (day.Rounds > 0 && peak > TimeSpan.Zero)
            {
                var ratio = (float)(day.Duration.Ticks / (double)peak.Ticks);
                var barHeight = MathF.Max(MinPlayedHeight * scale, plotHeight * ratio);

                DrawBar(canvas, centerX, baseline, barWidth, barHeight);
            }

            // Sağdan sola sayılıyor: hangi adım seçilirse seçilsin bugün her
            // zaman etiketli kalıyor, ebeveynin ilk baktığı yer orası.
            if ((days.Count - 1 - i) % labelStep == 0)
            {
                DrawWeekday(
                    canvas, centerX, width, baseline, day.Date, day.Date == today,
                    weekdayInitials, labelFont, scale);
            }
        }
    }

    /// <summary>
    /// Kaç günde bir gün adı yazılacağı.
    /// </summary>
    /// <remarks>
    /// Otuz günlük dönemde yuva daralıyor ve "Cmt" yanındakinin üstüne
    /// biniyor. Etiketi kırpmak ya da küçültmek yerine seyreltiliyor: haftada
    /// bir gün adı, çakışan otuz addan çok daha okunur. Ölçü tahmin değil,
    /// yazının gerçek genişliği.
    /// </remarks>
    private static int LabelStep(
        IReadOnlyList<string> names, SKFont font, float slot, float scale)
    {
        if (names.Count == 0 || slot <= 0f)
        {
            return 1;
        }

        var widest = names.Max(name => font.MeasureText(name));
        var needed = widest + (6f * scale);

        return needed <= slot ? 1 : (int)MathF.Ceiling(needed / slot);
    }

    private void DrawBaseline(SKCanvas canvas, float width, float baseline)
    {
        _rule.Color = PloofyPalette.Ink.WithAlpha(45);
        canvas.DrawLine(0, baseline, width, baseline, _rule);
    }

    /// <summary>
    /// En yoğun günün hizasında tek bir kılavuz çizgi ve değeri.
    /// </summary>
    /// <remarks>
    /// Tam bir y ekseni yerine tek çizgi: ölçeği dürüstçe veriyor (sıfır ile
    /// tepe arası) ve on dört sütunun üstüne dört rakam sırası bindirmiyor.
    /// Her sütuna değer yazmak, hiçbirinin okunmaması demek.
    /// </remarks>
    private void DrawPeakRule(
        SKCanvas canvas,
        float width,
        float top,
        TimeSpan peak,
        string minuteSuffix,
        SKFont font,
        float scale)
    {
        _rule.Color = PloofyPalette.Ink.WithAlpha(28);
        canvas.DrawLine(0, top, width, top, _rule);

        // Bir dakikanın altı da "1 dk": oynanmış bir günün tepesine "0 dk"
        // yazmak grafiği yalancı yapar.
        var minutes = Math.Max(1, (int)Math.Round(peak.TotalMinutes));

        _text.Color = PloofyPalette.Ink.WithAlpha(190);
        canvas.DrawText(
            $"{minutes} {minuteSuffix}",
            2f * scale,
            top - (7f * scale),
            SKTextAlign.Left,
            font,
            _text);
    }

    private void DrawBar(
        SKCanvas canvas, float centerX, float baseline, float barWidth, float barHeight)
    {
        var left = centerX - (barWidth / 2f);
        var right = centerX + (barWidth / 2f);
        var top = baseline - barHeight;

        // Kısa sütunda yarıçap yüksekliğin yarısını geçemiyor, yoksa uç
        // kendi üstüne kıvrılıp sütunu bir noktaya çeviriyor.
        var radius = MathF.Min(CapRadius, barHeight / 2f);

        using var path = new SKPath();
        path.MoveTo(left, baseline);
        path.LineTo(left, top + radius);
        path.QuadTo(left, top, left + radius, top);
        path.LineTo(right - radius, top);
        path.QuadTo(right, top, right, top + radius);
        path.LineTo(right, baseline);
        path.Close();

        _bar.Color = BarColor;
        canvas.DrawPath(path, _bar);
    }

    /// <summary>
    /// Gün harfi. Bugün koyu, diğerleri soluk.
    /// </summary>
    /// <remarks>
    /// Ebeveynin ilk baktığı şey sağ uç: "bugün oynadı mı". İşaretsiz bir
    /// eksende son sütunun bugün olduğu ancak sayılarak anlaşılıyor.
    /// </remarks>
    private void DrawWeekday(
        SKCanvas canvas,
        float centerX,
        float width,
        float baseline,
        DateOnly date,
        bool isToday,
        IReadOnlyList<string> initials,
        SKFont font,
        float scale)
    {
        if (initials.Count < 7)
        {
            return;
        }

        var text = initials[(int)date.DayOfWeek];

        // İlk ve son gün yuvanın ortasına yazılınca yazının yarısı grafiğin
        // dışına taşıyor ve kırpılıyor. Sütunun tam ortasında durmaktansa
        // birkaç piksel içeri kaymak iyi: kırpılan bir gün adı okunmuyor.
        var half = font.MeasureText(text) / 2f;
        var x = Math.Clamp(centerX, half + (1f * scale), width - half - (1f * scale));

        _text.Color = PloofyPalette.Ink.WithAlpha(isToday ? (byte)220 : (byte)105);
        canvas.DrawText(text, x, baseline + (15f * scale), SKTextAlign.Center, font, _text);
    }

    public void Dispose()
    {
        _bar.Dispose();
        _rule.Dispose();
        _text.Dispose();
    }
}
