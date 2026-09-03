using Ploofy.Engine.Difficulty;

namespace Ploofy.Engine.Games;

/// <summary>
/// Ayrılabilir bir kategori: adı arayüzde, üyeleri burada.
/// </summary>
/// <param name="Id">Sabit anahtar; arayüz adını ve simgesini buradan eşliyor.</param>
/// <param name="Items">
/// Kategorinin üyeleri. Emoji, çünkü üç dilde de aynı ve ek varlık
/// gerektirmiyor — aynı sebep avatarlarda da geçerli.
/// </param>
/// <param name="MinBand">
/// Bu kategorinin anlamlı olduğu en küçük bant. İnce ayrımlar (meyve /
/// sebze) yalnızca Meşe'de çıkıyor.
/// </param>
public sealed record ItemCategory(
    string Id,
    IReadOnlyList<string> Items,
    AgeBand MinBand = AgeBand.Filiz);

/// <summary>Ayrılacak tek parça.</summary>
public sealed record CategoryItem(int Id, string Glyph, string CategoryId);

/// <summary>Kategori Ayırma'nın kategorileri.</summary>
/// <remarks>
/// <para>
/// Kaba kategoriler (hayvan, araç, yiyecek, giysi) her bantta çıkıyor: iki
/// yaşındaki çocuk da bir kediyi bir arabadan ayırıyor. Meşe'ye ayrılan iki
/// kategori (meyve, sebze) <b>aynı üst kümenin</b> içinde ayrım yapmayı
/// istiyor ve zorluk oradan geliyor — daha çok kutudan değil.
/// </para>
/// <para>
/// Sınırda duran üyeler bilerek yok: domates ve avokado meyve/sebze
/// ayrımında yetişkinleri bile bölüyor, çocuğa yanlış cevap verdirtmenin
/// anlamı yok. Aynı sebeple hayvan kümesinde ejderha, araç kümesinde roket
/// bulunmuyor.
/// </para>
/// <para>
/// <b>Emojiler Unicode 10 ve öncesi.</b> Uygulamanın alt sınırı Android 8.0
/// ve daha yenisi orada boş kutu çıkıyor — aynı kural avatar kataloğunda da
/// geçerli. İlk yazımda soğan, sarımsak, marul, biber ve parmak arası
/// terlik bu kuralı çiğnemişti; hepsi listeden çıktı.
/// </para>
/// <para>
/// <c>CategorySortRoundTests</c> kaba bir güvenlik ağı kuruyor: U+1FA00 ve
/// üstündeki kod noktalarını reddediyor. O blok tümüyle Unicode 12 ve
/// sonrası, yani orada bir eşleşme kesinlikle hata. Blok Unicode 11
/// eklemelerini <b>yakalamıyor</b> (onlar eski blokların arasına serpilmiş);
/// tek gerçek doğrulama gerçek cihazda gözle bakmak.
/// </para>
/// </remarks>
public static class ItemCategories
{
    public static readonly IReadOnlyList<ItemCategory> All =
    [
        new("animals",
            ["🐶", "🐱", "🐰", "🐻", "🦊", "🐮", "🐷", "🐸", "🐵", "🦁"]),

        new("vehicles",
            ["🚗", "🚌", "🚂", "✈️", "🚁", "🚲", "🚜", "🛵", "🚑", "🚚"]),

        new("food",
            ["🍕", "🍞", "🧀", "🍔", "🥚", "🍪", "🥞", "🍟", "🌭", "🍰"]),

        new("clothes",
            ["👕", "👖", "🧥", "🧦", "👗", "🧢", "👟", "🧤", "👒", "👔"]),

        // --- Yalnızca Meşe: aynı üst kümenin içinde ayrım ---

        new("fruit",
            ["🍎", "🍌", "🍇", "🍓", "🍊", "🍐", "🍑", "🍒", "🥝", "🍍"],
            AgeBand.Mese),

        // Sekiz uye: bir turda kutu basina en cok dort parca dusuyor, yani
        // yeterli. Sogan, sarimsak, marul ve biber Unicode 11 ve sonrasi
        // olduklari icin listeye alinmadi - bkz. sinif aciklamasi.
        new("vegetables",
            ["🥕", "🥦", "🌽", "🥒", "🥔", "🍄", "🌶️", "🍆"],
            AgeBand.Mese),
    ];

    /// <summary>Bantta kullanılabilecek kategoriler.</summary>
    public static IReadOnlyList<ItemCategory> ForBand(AgeBand band) =>
        [.. All.Where(c => c.MinBand <= band)];

    public static ItemCategory? Find(string id) => All.FirstOrDefault(c => c.Id == id);
}

/// <summary>Kategori Ayırma'nın banda göre zorluk tablosu.</summary>
public static class CategorySortTuning
{
    /// <summary>Ekrandaki kutu sayısı.</summary>
    /// <remarks>
    /// Filiz'de iki: 2-4 yaşta üç seçenek arasında karar vermek, ayrımın
    /// kendisinden zor. Meşe'de üç — dört kutuya çıkmak yerine zorluk
    /// kategorilerin inceliğinden geliyor (meyve / sebze), çünkü dördüncü
    /// kutu yatay ekranda dokunma hedefini küçültüyor.
    /// </remarks>
    public static readonly BandValue<int> BinCount = new(2, 3, 3);

    /// <summary>Ayrılacak toplam parça sayısı.</summary>
    public static readonly BandValue<int> ItemCount = new(6, 9, 12);

    /// <summary>Yanlış kutu yıldızı düşürüyor mu?</summary>
    /// <remarks>
    /// Filiz'de hayır. O bantta yanlış kutu denemek öğrenmenin kendisi —
    /// Şekil Ayırma'da da aynı kural geçerli.
    /// </remarks>
    public static readonly BandValue<bool> CountsMistakes = new(false, true, true);

    /// <summary>Üçüncü yıldız için hedef süre (yalnızca Meşe).</summary>
    public static readonly BandValue<TimeSpan?> ParTime = new(
        null,
        null,
        TimeSpan.FromSeconds(60));
}

/// <summary>
/// Kategori Ayırma turu: parça hangi kümeye ait?
/// </summary>
/// <remarks>
/// <para>
/// Kurgusu Şekil Ayırma'nın aynısı — parçalar sırayla geliyor, ekranda tek
/// parça duruyor, yanlış kutu parçayı kaybettirmiyor — ama çalıştırdığı
/// beceri farklı. Şekil Ayırma <b>algısal</b> bir ayrım istiyor (üçgen mi
/// kare mi); burası <b>anlamsal</b>: kedi hayvan mı araç mı. İkincisi dilden
/// önce gelen bir sınıflandırma becerisi ve okumaya hazırlığın parçası.
/// </para>
/// <para>
/// Ortak motoru genelleştirmek yerine ikinci bir sınıf yazıldı. Sebep: iki
/// oyunun bant eksenleri farklı. Şekil Ayırma'da zorluk rengin şekille
/// birlikte gidip gitmemesinden geliyor, burada kategorilerin inceliğinden.
/// Ortak bir soyutlama ikisini de bulanıklaştırırdı.
/// </para>
/// <para>
/// Etkileşim <b>dokunma</b>, sürükleme değil: ekranda tek parça duruyor ve
/// çocuk kutuya dokunuyor. Sürükleme parmağın hassasiyetini sınardı, oysa
/// bu oyunun sorduğu tek şey kararın kendisi. Ayrıca parçalar emoji ve
/// emoji yalnızca MAUI etiketinde güvenilir çiziliyor; sürükleme bir Skia
/// yüzeyi gerektirirdi.
/// </para>
/// </remarks>
public sealed class CategorySortRound
{
    private readonly List<CategoryItem> _queue;

    private CategorySortRound(
        AgeBand band, IReadOnlyList<string> bins, List<CategoryItem> queue)
    {
        Band = band;
        Bins = bins;
        _queue = queue;
        Total = queue.Count;
        CountsMistakes = CategorySortTuning.CountsMistakes.For(band);
        ParTime = CategorySortTuning.ParTime.For(band);
    }

    /// <summary>
    /// Bant için bir tur kurar.
    /// </summary>
    /// <remarks>
    /// Meşe'de ince kategorilerden <b>en az biri</b> garanti: rastgele seçim
    /// bazen üç kaba kategori getiriyordu ve o tur Fidan'dan farksız
    /// oluyordu.
    /// </remarks>
    public static CategorySortRound ForBand(AgeBand band, Random? random = null)
    {
        var rng = random ?? Random.Shared;

        var binCount = CategorySortTuning.BinCount.For(band);
        var itemCount = CategorySortTuning.ItemCount.For(band);

        var pool = ItemCategories.ForBand(band);
        var fine = pool.Where(c => c.MinBand == AgeBand.Mese).ToList();

        var chosen = new List<ItemCategory>(binCount);

        if (fine.Count > 0 && binCount > 0)
        {
            chosen.Add(fine[rng.Next(fine.Count)]);
        }

        foreach (var category in pool.OrderBy(_ => rng.Next()))
        {
            if (chosen.Count >= binCount)
            {
                break;
            }

            if (!chosen.Contains(category))
            {
                chosen.Add(category);
            }
        }

        // Kutuların ekrandaki sırası da karışıyor: ince kategori hep başta
        // durursa çocuk sırayı ezberliyor.
        chosen = [.. chosen.OrderBy(_ => rng.Next())];

        // Her kutuya eşit sayıda parça. Eşitlik önemli: bir kategoriden tek
        // parça gelirse o kutu turun sonuna kadar boş duruyor ve çocuk
        // kutunun bozuk olduğunu sanıyor. Aynı gerekçe Şekil Ayırma'da da var.
        var perBin = Math.Max(1, itemCount / chosen.Count);
        var items = new List<CategoryItem>(perBin * chosen.Count);
        var id = 0;

        foreach (var category in chosen)
        {
            var picked = category.Items.OrderBy(_ => rng.Next()).Take(perBin);
            foreach (var glyph in picked)
            {
                items.Add(new CategoryItem(id++, glyph, category.Id));
            }
        }

        Shuffle(items, rng);
        SpreadOutRuns(items);

        return new CategorySortRound(band, [.. chosen.Select(c => c.Id)], items);
    }

    public AgeBand Band { get; }

    /// <summary>Ekrandaki kutular, soldan sağa. Değerler kategori anahtarı.</summary>
    public IReadOnlyList<string> Bins { get; }

    public bool CountsMistakes { get; }

    public TimeSpan? ParTime { get; }

    public int Total { get; }

    public int Sorted { get; private set; }

    /// <summary>Yanlış kutu sayısı, sayılıp sayılmadığından bağımsız.</summary>
    public int WrongDrops { get; private set; }

    /// <summary>Yıldız hesabına giden hata sayısı.</summary>
    public int Mistakes => CountsMistakes ? WrongDrops : 0;

    public int Remaining => _queue.Count;

    public bool IsComplete => _queue.Count == 0;

    /// <summary>Sıradaki parça; tur bittiyse null.</summary>
    public CategoryItem? Current => _queue.Count > 0 ? _queue[0] : null;

    /// <summary>Sıradan sonraki parça — arayüz onu arkada soluk gösteriyor.</summary>
    public CategoryItem? Next => _queue.Count > 1 ? _queue[1] : null;

    /// <summary>
    /// Sıradaki parçayı bir kutuya koyar.
    /// </summary>
    /// <remarks>
    /// Yanlış kutuda parça <b>kaybolmuyor</b>, sıranın başında kalıyor:
    /// çocuk aynı parçayı doğru kutuya koyana kadar deneyebiliyor.
    /// </remarks>
    public DropOutcome Drop(string categoryId)
    {
        if (Current is not { } item || !Bins.Contains(categoryId))
        {
            return DropOutcome.Ignored;
        }

        if (item.CategoryId != categoryId)
        {
            WrongDrops++;
            return DropOutcome.WrongBin;
        }

        _queue.RemoveAt(0);
        Sorted++;
        return DropOutcome.Sorted;
    }

    private static void Shuffle<T>(IList<T> items, Random rng)
    {
        for (var i = items.Count - 1; i > 0; i--)
        {
            var j = rng.Next(i + 1);
            (items[i], items[j]) = (items[j], items[i]);
        }
    }

    /// <summary>
    /// Üç ve daha uzun aynı-kategori dizilerini böler.
    /// </summary>
    /// <remarks>
    /// Sıra rastgele olduğu hâlde çocuk "hep aynı kutu" hissine kapılmasın.
    /// </remarks>
    private static void SpreadOutRuns(List<CategoryItem> items)
    {
        for (var i = 2; i < items.Count; i++)
        {
            if (items[i].CategoryId != items[i - 1].CategoryId
                || items[i].CategoryId != items[i - 2].CategoryId)
            {
                continue;
            }

            for (var j = i + 1; j < items.Count; j++)
            {
                if (items[j].CategoryId == items[i].CategoryId)
                {
                    continue;
                }

                (items[i], items[j]) = (items[j], items[i]);
                break;
            }
        }
    }
}
