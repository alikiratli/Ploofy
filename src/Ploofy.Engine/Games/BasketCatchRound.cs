using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>
/// Düşen tek nesne.
/// </summary>
/// <remarks>
/// Konumlar 0-1 arasında normalleştirilmiş — Balon Patlatma'daki kuralın
/// aynısı: motor ekranın kaç piksel olduğunu bilmiyor, aynı tur telefonda ve
/// tablette aynı zorlukta oluyor. Değiştirilebilir bir sınıf, kayıt değil:
/// saniyede 60 kez güncellenen nesneleri her karede yeniden üretmek bu yaş
/// grubunun cihazlarında gereksiz çöp topluyor.
/// </remarks>
public sealed class FallingItem
{
    public int Id { get; init; }

    public ShapeKind Kind { get; init; }

    public BubbleHue Hue { get; init; }

    /// <summary>Yatay merkez (0 sol, 1 sağ) — savrulma dahil.</summary>
    public float X { get; internal set; }

    /// <summary>Dikey merkez (0 üst kenar, 1 alt kenar).</summary>
    public float Y { get; internal set; }

    /// <summary>Yarıçap, ekran genişliğine oranla.</summary>
    public float Radius { get; init; }

    /// <summary>Saniyede kat edilen dikey mesafe.</summary>
    public float Speed { get; init; }

    /// <summary>Çizim katmanının döndürme animasyonu için — hepsi aynı açıda durmasın.</summary>
    public float Spin { get; init; }

    /// <summary>Sepetin ağzından geçti mi? Yakalama bir kez sınanıyor.</summary>
    internal bool HasPassed { get; set; }

    internal float BaseX { get; init; }

    internal float DriftAmplitude { get; init; }

    internal float DriftSpeed { get; init; }

    internal float DriftPhase { get; set; }
}

/// <summary>Bir nesnenin akıbeti — arayüz sesi ve parçacığı buradan sürüyor.</summary>
public readonly record struct CatchEvent(
    int ItemId,
    float X,
    float Y,
    ShapeKind Kind,
    BubbleHue Hue,
    bool Caught);

/// <summary>Sepeti Tut'un banda göre zorluk tablosu.</summary>
/// <remarks>
/// Oyun katalogda Fidan'dan itibaren görünüyor ama tablo yine üç bant
/// taşıyor: motor bandı varsayamaz ve Filiz sütunu bir gün gerekirse
/// dengelenmiş hâlde hazır dursun.
/// </remarks>
public static class BasketCatchTuning
{
    /// <summary>Turu bitirmek için yakalanması gereken nesne sayısı.</summary>
    public static readonly BandValue<int> Goal = new(8, 10, 14);

    /// <summary>Sepetin genişliği, ekran genişliğine oranla.</summary>
    /// <remarks>
    /// Zorluğun yarısı burada. Geniş sepet kabaca doğru yerde durmayı
    /// yetiyor kılıyor; dar sepet nesnenin <b>nereye düşeceğini</b> önceden
    /// kestirmeyi gerektiriyor ve oyunun asıl becerisi o.
    /// </remarks>
    public static readonly BandValue<float> BasketWidth = new(0.34f, 0.27f, 0.20f);

    /// <summary>Düşme hızı (ekran yüksekliğinin saniyedeki oranı).</summary>
    public static readonly BandValue<float> FallSpeed = new(0.26f, 0.36f, 0.50f);

    /// <summary>İki nesne arasındaki süre.</summary>
    public static readonly BandValue<TimeSpan> SpawnInterval = new(
        TimeSpan.FromMilliseconds(1150),
        TimeSpan.FromMilliseconds(880),
        TimeSpan.FromMilliseconds(620));

    /// <summary>Ekranda aynı anda düşebilecek en fazla nesne.</summary>
    public static readonly BandValue<int> MaxItems = new(3, 4, 6);

    public static readonly BandValue<float> ItemRadius = new(0.075f, 0.065f, 0.055f);

    /// <summary>
    /// Nesnelerin düşerken yana savrulma genliği.
    /// </summary>
    /// <remarks>
    /// Düz düşen bir nesnenin nereye ineceği ilk karede belli; savrulan
    /// nesneninki değil. Bu yüzden savrulma yalnızca Meşe'de var — altındaki
    /// bantta oyun "sepeti doğru sütuna götür"den ibaret kalıyor ve bu da o
    /// yaş için doğru olan.
    /// </remarks>
    public static readonly BandValue<float> Drift = new(0f, 0f, 0.055f);

    /// <summary>Kaçırmak hata sayılıyor mu?</summary>
    /// <remarks>
    /// Yalnızca Meşe'de. Altındaki bantlarda kaçan nesne sessizce yere
    /// düşüyor ve bir sonraki geliyor — o bantlarda kaybetme yok.
    /// </remarks>
    public static readonly BandValue<bool> CountsMisses = new(false, false, true);

    // Hedef süre yok. Turun temposunu çocuk değil doğma aralığı belirliyor:
    // hızlı oynamak turu kısaltmıyor, çünkü nesnelerin düşmesini beklemek
    // zorunlu. Meşe'nin üçüncü yıldızı bu oyunda kaçırmamaya bağlı.
    // Aynı gerekçe Sırayı Tekrarla'da da geçerli; bkz. SimonTuning.
}

/// <summary>
/// Sepeti Tut oyununun bir turu — çizimden bağımsız kurallar.
/// </summary>
/// <remarks>
/// <para>
/// Yukarıdan nesneler düşüyor, çocuk aşağıdaki sepeti sağa sola kaydırıp
/// onları yakalıyor. Kütüphanedeki tek <b>sürekli takip</b> oyunu: Balon
/// Patlatma'da hedef bekliyor, burada hedef geliyor ve nereye ineceğini
/// önceden kestirmek gerekiyor.
/// </para>
/// <para>
/// Yakalanmayacak nesne <b>yok</b>. Düşenlerin bir kısmını "alma" yapmak
/// oyuna ikinci bir kural katardı ve asıl beceriyi — el ile gözün birlikte
/// çalışması — bulanıklaştırırdı. Zorluk sepetin darlığından, düşme hızından
/// ve savrulmadan geliyor.
/// </para>
/// </remarks>
public sealed class BasketCatchRound
{
    /// <summary>Sepetin ağzının bulunduğu yükseklik.</summary>
    /// <remarks>
    /// Motorda sabit çünkü yakalama kuralı buna bağlı; arayüz sepeti tam bu
    /// çizgiye çiziyor. İki yerde ayrı ayrı tanımlanırsa ekranda değen bir
    /// nesne motorda ıskalanmış sayılıyor.
    /// </remarks>
    public const float CatchLine = 0.82f;

    private readonly Random _rng;
    private readonly List<FallingItem> _items = [];
    private readonly List<CatchEvent> _events = [];
    private readonly ShapeKind[] _kinds = Enum.GetValues<ShapeKind>();
    private readonly BubbleHue[] _hues = Enum.GetValues<BubbleHue>();

    private TimeSpan _sinceLastSpawn;
    private int _nextId;

    private BasketCatchRound(AgeBand band, Random rng)
    {
        Band = band;
        _rng = rng;

        Goal = BasketCatchTuning.Goal.For(band);
        BasketWidth = BasketCatchTuning.BasketWidth.For(band);
        FallSpeed = BasketCatchTuning.FallSpeed.For(band);
        SpawnInterval = BasketCatchTuning.SpawnInterval.For(band);
        MaxItems = BasketCatchTuning.MaxItems.For(band);
        ItemRadius = BasketCatchTuning.ItemRadius.For(band);
        Drift = BasketCatchTuning.Drift.For(band);
        CountsMisses = BasketCatchTuning.CountsMisses.For(band);

        BasketX = 0.5f;

        // İlk nesne ekranın hemen üstünde hazır bekliyor: boş bir ekrana
        // bakarak beklemek bu yaş grubunda oyunun başlamadığı hissini veriyor.
        Spawn();
    }

    public static BasketCatchRound ForBand(AgeBand band, Random? random = null) =>
        new(band, random ?? Random.Shared);

    public AgeBand Band { get; }

    /// <summary>Yakalanması gereken nesne sayısı.</summary>
    public int Goal { get; }

    public float BasketWidth { get; }

    /// <summary>Sepetin yatay merkezi (0 sol, 1 sağ).</summary>
    public float BasketX { get; private set; }

    public float FallSpeed { get; }

    public TimeSpan SpawnInterval { get; }

    public int MaxItems { get; }

    public float ItemRadius { get; }

    public float Drift { get; }

    /// <summary>Kaçırmak hata sayılıyor mu? Yalnızca Meşe'de doğru.</summary>
    public bool CountsMisses { get; }

    public IReadOnlyList<FallingItem> Items => _items;

    /// <summary>
    /// Son <see cref="Advance"/> çağrısında olan yakalama ve kaçırmalar.
    /// </summary>
    /// <remarks>
    /// Her karede yeni bir liste döndürmek yerine aynı liste temizlenip
    /// dolduruluyor: karelerin çoğunda hiç olay yok ve saniyede 60 boş liste
    /// üretmenin anlamı yok.
    /// </remarks>
    public IReadOnlyList<CatchEvent> LastEvents => _events;

    public TimeSpan Elapsed { get; private set; }

    public int Caught { get; private set; }

    /// <summary>Kaçan nesne sayısı — hata sayılıp sayılmadığından bağımsız.</summary>
    public int Missed { get; private set; }

    /// <summary>Yıldız hesabına giden hata sayısı.</summary>
    public int Mistakes => CountsMisses ? Missed : 0;

    public bool IsComplete => Caught >= Goal;

    /// <summary>
    /// Sepeti verilen yatay konuma taşır.
    /// </summary>
    /// <remarks>
    /// Konum kenarlara sıkıştırılıyor: sepetin yarısı ekran dışına çıkarsa
    /// oradan düşen nesne yakalanamaz hâle geliyor ve çocuk bunu göremiyor.
    /// </remarks>
    public void MoveBasketTo(float x)
    {
        var half = BasketWidth / 2f;
        BasketX = Math.Clamp(x, half, 1f - half);
    }

    /// <summary>
    /// Oyunu bir kare ilerletir: nesneler düşer, sepete girenler yakalanır,
    /// yere düşenler kaçar, zamanı gelen yeni nesne doğar.
    /// </summary>
    public void Advance(TimeSpan delta)
    {
        _events.Clear();

        if (IsComplete)
        {
            return;
        }

        Elapsed += delta;
        var seconds = (float)delta.TotalSeconds;

        for (var i = _items.Count - 1; i >= 0; i--)
        {
            var item = _items[i];

            item.Y += item.Speed * seconds;
            item.DriftPhase += item.DriftSpeed * seconds;
            item.X = item.BaseX + (item.DriftAmplitude * MathF.Sin(item.DriftPhase));

            // Yakalama tam bir kez, ağzı geçerken sınanıyor. Her karede
            // sınamak, sepetin altından geçen nesnenin sepet oraya sonradan
            // gelince yakalanmasına yol açıyordu.
            if (!item.HasPassed && item.Y >= CatchLine)
            {
                item.HasPassed = true;

                if (IsOverBasket(item))
                {
                    Caught++;
                    _events.Add(new CatchEvent(item.Id, item.X, item.Y, item.Kind, item.Hue, true));
                    _items.RemoveAt(i);
                    continue;
                }
            }

            // Kaçan nesne ağzı geçtikten sonra da düşmeye devam ediyor;
            // ekrandan çıkınca sayılıyor. Havada yok olsaydı çocuk neyi
            // kaçırdığını göremezdi.
            if (item.Y - item.Radius > 1f)
            {
                Missed++;
                _events.Add(new CatchEvent(item.Id, item.X, item.Y, item.Kind, item.Hue, false));
                _items.RemoveAt(i);
            }
        }

        _sinceLastSpawn += delta;
        if (_sinceLastSpawn >= SpawnInterval && _items.Count < MaxItems)
        {
            _sinceLastSpawn = TimeSpan.Zero;
            Spawn();
        }
    }

    /// <summary>
    /// Nesne sepetin ağzında mı?
    /// </summary>
    /// <remarks>
    /// İsabet alanı sepetin yarısından biraz geniş: nesnenin kenarı sepetin
    /// kenarına değdiğinde de içine düşmüş sayılıyor. Çizilen ile sayılanın
    /// birebir aynı olması bu yaşta oyunu cimri gösteriyor.
    /// </remarks>
    private bool IsOverBasket(FallingItem item) =>
        MathF.Abs(item.X - BasketX) <= (BasketWidth / 2f) + (item.Radius * 0.6f);

    private void Spawn()
    {
        var radius = ItemRadius * (0.9f + ((float)_rng.NextDouble() * 0.2f));

        // Savrulma genliği de kenar payına giriyor, yoksa savrulan nesne
        // ekranın dışına salınıp geri geliyor.
        var margin = radius + Drift;
        var baseX = margin + ((float)_rng.NextDouble() * (1f - (2f * margin)));

        _items.Add(new FallingItem
        {
            Id = _nextId++,
            Kind = _kinds[_rng.Next(_kinds.Length)],
            Hue = _hues[_rng.Next(_hues.Length)],
            BaseX = baseX,
            X = baseX,
            Y = -radius,
            Radius = radius,
            Speed = FallSpeed * (0.9f + ((float)_rng.NextDouble() * 0.2f)),
            Spin = ((float)_rng.NextDouble() - 0.5f) * 0.6f,
            DriftAmplitude = Drift * (float)_rng.NextDouble(),
            DriftSpeed = 1.1f + ((float)_rng.NextDouble() * 1.4f),
            DriftPhase = (float)_rng.NextDouble() * MathF.Tau,
        });
    }
}
