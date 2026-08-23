using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>
/// Balonun rengi.
/// </summary>
/// <remarks>
/// Renk adı değil kimlik: arayüz her renge kendi degradesini eşliyor, motor
/// yalnızca "aynı mı, farklı mı" sorusunu cevaplıyor. Altı renk, birbirinden
/// tonla değil ton ailesiyle ayrılıyor — renk körlüğü olan çocuk da balonları
/// birbirinden ayırabilsin diye.
/// </remarks>
public enum BubbleHue
{
    Cherry,
    Ocean,
    Lime,
    Sunny,
    Grape,
    Bubblegum,
}

/// <summary>Bir balona dokunmanın sonucu.</summary>
public enum PopOutcome
{
    /// <summary>Boşluğa dokunuldu.</summary>
    Miss,

    /// <summary>Doğru balon patladı.</summary>
    Popped,

    /// <summary>
    /// Hedef dışı renge dokunuldu. Filiz bandında bu sonuç hiç üretilmiyor —
    /// o bantta her balon doğru balon.
    /// </summary>
    WrongColor,
}

/// <summary>
/// Ekrandaki tek balon.
/// </summary>
/// <remarks>
/// Konumlar 0-1 arasında normalleştirilmiş: motor ekranın kaç piksel olduğunu
/// bilmiyor, çizim katmanı ölçekliyor. Aynı tur hem telefonda hem tablette
/// aynı zorlukta oluyor.
/// <para>
/// Değiştirilebilir bir sınıf, kayıt değil: saniyede 60 kez güncellenen
/// nesneleri her karede yeniden üretmek bu yaş grubunun cihazlarında
/// gereksiz çöp topluyor.
/// </para>
/// </remarks>
public sealed class Bubble
{
    public int Id { get; init; }

    public BubbleHue Hue { get; init; }

    /// <summary>Yatay merkez (0 sol, 1 sağ) — salınım dahil.</summary>
    public float X { get; internal set; }

    /// <summary>Dikey merkez (1 alt kenar, 0 üst kenar).</summary>
    public float Y { get; internal set; }

    /// <summary>Yarıçap, ekran genişliğine oranla.</summary>
    public float Radius { get; init; }

    /// <summary>Saniyede kat edilen dikey mesafe.</summary>
    public float Speed { get; init; }

    /// <summary>Salınımın merkez ekseni.</summary>
    internal float BaseX { get; init; }

    internal float WobbleAmplitude { get; init; }

    internal float WobbleSpeed { get; init; }

    internal float WobblePhase { get; set; }

    /// <summary>
    /// Çizim katmanının nefes alma/esneme animasyonunu balonlar arasında
    /// kaydırması için. Hepsi aynı anda nefes alırsa mekanik görünüyor.
    /// </summary>
    public float AnimationOffset { get; init; }
}

/// <summary>
/// Balon Patlatma'nın banda göre zorluk tablosu.
/// </summary>
public static class BubblePopTuning
{
    /// <summary>Turu bitirmek için patlatılması gereken balon sayısı.</summary>
    public static readonly BandValue<int> Goal = new(10, 8, 12);

    /// <summary>
    /// Balon yarıçapı. Küçük yaşta büyük: bu bantta zorluk balonu bulmak
    /// değil, parmağını denk getirmek.
    /// </summary>
    public static readonly BandValue<float> Radius = new(0.135f, 0.105f, 0.085f);

    /// <summary>Yükselme hızı (ekran yüksekliğinin saniyedeki oranı).</summary>
    public static readonly BandValue<float> Speed = new(0.055f, 0.080f, 0.115f);

    /// <summary>İki balon arasındaki süre.</summary>
    public static readonly BandValue<TimeSpan> SpawnInterval = new(
        TimeSpan.FromMilliseconds(900),
        TimeSpan.FromMilliseconds(720),
        TimeSpan.FromMilliseconds(520));

    /// <summary>Ekranda aynı anda durabilecek en fazla balon.</summary>
    public static readonly BandValue<int> MaxBubbles = new(5, 8, 11);

    /// <summary>
    /// Hedef renk var mı? Filiz'de yok: o bantta oyun bir görev değil, dokun-
    /// patlat keşfi. Hedef renk harf/sayı tanımadan önceki ilk "kurala uy"
    /// adımı, o yüzden Fidan'da başlıyor.
    /// </summary>
    public static readonly BandValue<bool> HasTargetColor = new(false, true, true);

    /// <summary>Kaç farklı renk dolaşıyor? Az renk, hedefi bulmayı kolaylaştırıyor.</summary>
    public static readonly BandValue<int> PaletteSize = new(6, 3, 5);

    /// <summary>Süre sınırı — yalnızca Meşe.</summary>
    public static readonly BandValue<TimeSpan?> TimeLimit = new(
        null,
        null,
        TimeSpan.FromSeconds(45));

    /// <summary>Üçüncü yıldız için hedef süre.</summary>
    public static readonly BandValue<TimeSpan?> ParTime = new(
        null,
        null,
        TimeSpan.FromSeconds(30));
}

/// <summary>
/// Balon Patlatma oyununun bir turu — çizimden bağımsız kurallar.
/// </summary>
/// <remarks>
/// Balonların doğması, yükselmesi, ekrandan çıkması ve patlaması burada;
/// nasıl göründükleri çizim katmanında. Konumlar normalleştirilmiş olduğu
/// için kurallar ekran boyutundan bağımsız ve testlerde saat elle ilerletilerek
/// doğrulanabiliyor.
/// </remarks>
public sealed class BubblePopRound
{
    private readonly Random _rng;
    private readonly List<Bubble> _bubbles = [];
    private readonly BubbleHue[] _palette;

    private TimeSpan _sinceLastSpawn;
    private int _nextId;

    private BubblePopRound(AgeBand band, Random rng)
    {
        Band = band;
        _rng = rng;

        Goal = BubblePopTuning.Goal.For(band);
        BubbleRadius = BubblePopTuning.Radius.For(band);
        RiseSpeed = BubblePopTuning.Speed.For(band);
        SpawnInterval = BubblePopTuning.SpawnInterval.For(band);
        MaxBubbles = BubblePopTuning.MaxBubbles.For(band);
        TimeLimit = BubblePopTuning.TimeLimit.For(band);
        ParTime = BubblePopTuning.ParTime.For(band);

        var paletteSize = BubblePopTuning.PaletteSize.For(band);
        _palette = Enum.GetValues<BubbleHue>()
            .OrderBy(_ => rng.Next())
            .Take(paletteSize)
            .ToArray();

        TargetHue = BubblePopTuning.HasTargetColor.For(band)
            ? _palette[rng.Next(_palette.Length)]
            : null;

        // İlk balonlar ekranda hazır bulunsun: boş bir ekrana bakarak
        // beklemek bu yaş grubunda oyunun başlamadığı hissini veriyor.
        for (var i = 0; i < Math.Min(3, MaxBubbles); i++)
        {
            Spawn(startY: 1.05f + (i * 0.22f));
        }
    }

    public static BubblePopRound ForBand(AgeBand band, Random? random = null) =>
        new(band, random ?? Random.Shared);

    public AgeBand Band { get; }

    /// <summary>Patlatılması gereken balon sayısı.</summary>
    public int Goal { get; }

    /// <summary>Hedef renk; Filiz bandında null (her balon sayılır).</summary>
    public BubbleHue? TargetHue { get; }

    public float BubbleRadius { get; }

    public float RiseSpeed { get; }

    public TimeSpan SpawnInterval { get; }

    public int MaxBubbles { get; }

    public TimeSpan? TimeLimit { get; }

    public TimeSpan? ParTime { get; }

    public IReadOnlyList<Bubble> Bubbles => _bubbles;

    /// <summary>Turdaki toplam geçen süre.</summary>
    public TimeSpan Elapsed { get; private set; }

    public int Popped { get; private set; }

    public int Mistakes { get; private set; }

    public bool IsComplete => Popped >= Goal;

    /// <summary>
    /// Süre doldu mu? Yalnızca Meşe'de anlamlı; diğer bantlarda hep false,
    /// çünkü orada kaybetme yok.
    /// </summary>
    public bool IsTimeUp => TimeLimit is { } limit && Elapsed >= limit;

    public bool IsOver => IsComplete || IsTimeUp;

    /// <summary>Kalan süre; süre sınırı yoksa null.</summary>
    public TimeSpan? Remaining => TimeLimit is { } limit
        ? (limit > Elapsed ? limit - Elapsed : TimeSpan.Zero)
        : null;

    /// <summary>
    /// Oyunu bir kare ilerletir: balonlar yükselir, ekrandan çıkanlar silinir,
    /// zamanı gelen yeni balon doğar.
    /// </summary>
    public void Advance(TimeSpan delta)
    {
        if (IsOver)
        {
            return;
        }

        Elapsed += delta;
        var seconds = (float)delta.TotalSeconds;

        for (var i = _bubbles.Count - 1; i >= 0; i--)
        {
            var bubble = _bubbles[i];
            bubble.Y -= bubble.Speed * seconds;
            bubble.WobblePhase += bubble.WobbleSpeed * seconds;
            bubble.X = bubble.BaseX + (bubble.WobbleAmplitude * MathF.Sin(bubble.WobblePhase));

            // Üstten çıkan balon kaçtı. Kaçırmak hata sayılmıyor: hedefi
            // ıskalamak ile yanlış renge dokunmak farklı şeyler ve bu yaşta
            // ikincisi bile ancak Fidan'dan sonra sayılıyor.
            if (bubble.Y + bubble.Radius < 0f)
            {
                _bubbles.RemoveAt(i);
            }
        }

        _sinceLastSpawn += delta;
        if (_sinceLastSpawn >= SpawnInterval && _bubbles.Count < MaxBubbles)
        {
            _sinceLastSpawn = TimeSpan.Zero;
            Spawn();
        }

        EnsureTargetIsReachable();
    }

    /// <summary>
    /// Verilen noktaya dokunur. Üst üste binen balonlarda en öndeki (en son
    /// doğan) patlar — çocuğun gördüğü balon o.
    /// </summary>
    public PopOutcome PopAt(float x, float y)
    {
        if (IsOver)
        {
            return PopOutcome.Miss;
        }

        for (var i = _bubbles.Count - 1; i >= 0; i--)
        {
            var bubble = _bubbles[i];
            var dx = x - bubble.X;
            var dy = y - bubble.Y;

            // Dokunma yarıçapı çizilenden bir miktar geniş: küçük çocuğun
            // parmağı balonun kenarına değdiğinde de patlaması gerekiyor.
            var reach = bubble.Radius * 1.25f;
            if ((dx * dx) + (dy * dy) > reach * reach)
            {
                continue;
            }

            if (TargetHue is { } target && bubble.Hue != target)
            {
                Mistakes++;
                return PopOutcome.WrongColor;
            }

            _bubbles.RemoveAt(i);
            Popped++;
            return PopOutcome.Popped;
        }

        return PopOutcome.Miss;
    }

    /// <summary>Patlayan balonun yerine hemen yenisi gelsin diye.</summary>
    private void Spawn(float? startY = null)
    {
        var radius = BubbleRadius * (0.85f + ((float)_rng.NextDouble() * 0.3f));
        var baseX = radius + ((float)_rng.NextDouble() * (1f - (2f * radius)));

        _bubbles.Add(new Bubble
        {
            Id = _nextId++,
            Hue = _palette[_rng.Next(_palette.Length)],
            BaseX = baseX,
            X = baseX,
            Y = startY ?? (1f + radius),
            Radius = radius,
            Speed = RiseSpeed * (0.85f + ((float)_rng.NextDouble() * 0.3f)),
            WobbleAmplitude = 0.012f + ((float)_rng.NextDouble() * 0.03f),
            WobbleSpeed = 0.8f + ((float)_rng.NextDouble() * 1.2f),
            WobblePhase = (float)_rng.NextDouble() * MathF.Tau,
            AnimationOffset = (float)_rng.NextDouble() * MathF.Tau,
        });
    }

    /// <summary>
    /// Hedef renkten hiç balon kalmadıysa bir tane doğurur.
    /// </summary>
    /// <remarks>
    /// Rastgelelik bazen uzun süre hedef rengi hiç getirmiyor; çocuk o sırada
    /// doğru oynadığı hâlde ilerleyemiyor ve oyun bozuk sanılıyor. Bu, şansın
    /// oyunu kilitlemesini engelleyen tek müdahale.
    /// </remarks>
    private void EnsureTargetIsReachable()
    {
        if (TargetHue is not { } target || _bubbles.Count == 0)
        {
            return;
        }

        if (_bubbles.Any(b => b.Hue == target) || _bubbles.Count >= MaxBubbles)
        {
            return;
        }

        var radius = BubbleRadius;
        var baseX = radius + ((float)_rng.NextDouble() * (1f - (2f * radius)));

        _bubbles.Add(new Bubble
        {
            Id = _nextId++,
            Hue = target,
            BaseX = baseX,
            X = baseX,
            Y = 1f + radius,
            Radius = radius,
            Speed = RiseSpeed,
            WobbleAmplitude = 0.02f,
            WobbleSpeed = 1f,
            WobblePhase = (float)_rng.NextDouble() * MathF.Tau,
            AnimationOffset = (float)_rng.NextDouble() * MathF.Tau,
        });
    }
}
