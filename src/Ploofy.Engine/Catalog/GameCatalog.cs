using System.Collections.ObjectModel;

namespace Ploofy.Engine.Catalog;

/// <summary>
/// Uygulamadaki bütün mini oyunların kaydı.
/// </summary>
/// <remarks>
/// Yeni oyun eklemek iki adım: buraya bir satır, arayüz tarafında id'ye
/// karşılık bir sayfa. Kilit, bant filtresi, ilerleme kaydı ve ebeveyn ekranı
/// bu listeden beslendiği için başka hiçbir yere dokunmak gerekmez.
/// </remarks>
public static class GameCatalog
{
    public const string MemoryMatch = "memory_match";
    public const string BubblePop = "bubble_pop";
    public const string ShapeSort = "shape_sort";
    public const string MazeTrace = "maze_trace";
    public const string Jigsaw = "jigsaw";
    public const string SimonSequence = "simon_sequence";
    public const string BasketCatch = "basket_catch";
    public const string LetterHunt = "letter_hunt";
    public const string NumberHunt = "number_hunt";
    public const string CountMatch = "count_match";
    public const string LetterTrace = "letter_trace";
    public const string Pattern = "pattern";
    public const string LineUp = "line_up";
    public const string DotToDot = "dot_to_dot";
    public const string CategorySort = "category_sort";
    public const string Addition = "addition";

    public static readonly ReadOnlyCollection<MiniGameDescriptor> Games = new([

        // --- Eğlendirici oyunlar ---

        // Ücretsiz vitrin 1: en tanıdık mekanik, açıklama gerektirmiyor.
        new MiniGameDescriptor(
            MemoryMatch,
            InteractionKind.Memory,
            GameTier.Free,
            AgeBand.Filiz,
            RenderKind.Layout,
            SupportsPassAndPlay: true),

        // Ücretsiz vitrin 2: Filiz bandında amaçsız/sakinleştirici mod olarak da
        // çalışır, yani en küçük yaş grubu ilk açılışta hemen bir şey oynayabilir.
        new MiniGameDescriptor(
            BubblePop,
            InteractionKind.Tap,
            GameTier.Free,
            AgeBand.Filiz,
            RenderKind.Canvas),

        new MiniGameDescriptor(
            ShapeSort,
            InteractionKind.Drag,
            GameTier.Subscription,
            AgeBand.Filiz,
            RenderKind.Layout,
            SupportsPassAndPlay: true),

        new MiniGameDescriptor(
            MazeTrace,
            InteractionKind.Trace,
            GameTier.Subscription,
            AgeBand.Filiz,
            RenderKind.Canvas),

        new MiniGameDescriptor(
            Jigsaw,
            InteractionKind.Drag,
            GameTier.Subscription,
            AgeBand.Filiz,
            RenderKind.Canvas,
            SupportsPassAndPlay: true),

        new MiniGameDescriptor(
            SimonSequence,
            InteractionKind.Sequence,
            GameTier.Subscription,
            AgeBand.Filiz,
            RenderKind.Layout,
            SupportsPassAndPlay: true),

        new MiniGameDescriptor(
            BasketCatch,
            InteractionKind.Drag,
            GameTier.Subscription,
            AgeBand.Fidan,
            RenderKind.Canvas,
            SupportsPassAndPlay: true),

        // --- Öğretici oyunlar ---
        // Harf ve sayı ayrı oyunlar: aynı mekaniği paylaşsalar da çocuk "harf
        // oyunu"nu ve "sayı oyunu"nu ayrı seçmek istiyor, ebeveyn de neyin
        // çalışıldığını görmek istiyor.

        new MiniGameDescriptor(
            LetterHunt,
            InteractionKind.Tap,
            GameTier.Subscription,
            AgeBand.Fidan,
            RenderKind.Layout,
            IsEducational: true,
            SupportsPassAndPlay: true),

        new MiniGameDescriptor(
            NumberHunt,
            InteractionKind.Tap,
            GameTier.Subscription,
            AgeBand.Fidan,
            RenderKind.Layout,
            IsEducational: true,
            SupportsPassAndPlay: true),

        // Miktar ile rakamı eşleştirme — sayı tanımanın bir sonraki adımı.
        new MiniGameDescriptor(
            CountMatch,
            InteractionKind.Drag,
            GameTier.Subscription,
            AgeBand.Fidan,
            RenderKind.Layout,
            IsEducational: true,
            SupportsPassAndPlay: true),

        // Harf ve rakam yazma. En küçük bant Fidan: 2-4 yaş harf yazmıyor,
        // o yaşın yazı öncesi karşılığı Yolu Bul.
        //
        // Sıralı oyunu desteklemiyor: bir harfi yazmak on saniye sürüyor ve
        // her harfte cihazı elden ele vermek oyunu devir ekranına çeviriyor.
        new MiniGameDescriptor(
            LetterTrace,
            InteractionKind.Trace,
            GameTier.Subscription,
            AgeBand.Fidan,
            RenderKind.Canvas,
            IsEducational: true),

        // Örüntü tamamlama. En küçük bant Filiz: o bantta yalnızca AB
        // örüntüsü ve renk değişimi var, ki amaç örüntü kurmak değil
        // "bir şey tekrar ediyor" fikrini yakalamak.
        new MiniGameDescriptor(
            Pattern,
            InteractionKind.Tap,
            GameTier.Subscription,
            AgeBand.Filiz,
            RenderKind.Layout,
            IsEducational: true,
            SupportsPassAndPlay: true),

        // Sıralama ve karşılaştırma. Filiz boyuta bakıyor (saymak yok),
        // Fidan'dan itibaren miktara — Say ve Eşleştir'in devamı.
        new MiniGameDescriptor(
            LineUp,
            InteractionKind.Drag,
            GameTier.Subscription,
            AgeBand.Filiz,
            RenderKind.Canvas,
            IsEducational: true,
            SupportsPassAndPlay: true),

        // Noktaları birleştirme. Sayı Avı rakamı tanıtıyor, Say ve Eşleştir
        // miktarla eşliyor; burada çalışılan şey sıra — birden sonra iki
        // gelir. En küçük bant Fidan: 2-4 yaş rakam sırası takip etmiyor.
        //
        // Sıralı oyunu desteklemiyor: bir resim baştan sona tek bir çizim ve
        // yarısında cihazı elden ele vermek çizimi ikiye bölerdi.
        new MiniGameDescriptor(
            DotToDot,
            InteractionKind.Tap,
            GameTier.Subscription,
            AgeBand.Fidan,
            RenderKind.Canvas,
            IsEducational: true),

        // Kategori ayırma. Şekil Ayırma algısal bir ayrım istiyor (üçgen mi
        // kare mi); burası anlamsal: kedi hayvan mı araç mı. Sınıflandırma
        // dilden önce gelen bir beceri ve okumaya hazırlığın parçası, o
        // yüzden öğretici tarafta.
        //
        // Dokunma, sürükleme değil: ekranda tek parça duruyor ve çocuk
        // kutuya dokunuyor. Sorulan tek şey kararın kendisi, parmağın
        // hassasiyeti değil.
        new MiniGameDescriptor(
            CategorySort,
            InteractionKind.Tap,
            GameTier.Subscription,
            AgeBand.Filiz,
            RenderKind.Layout,
            IsEducational: true,
            SupportsPassAndPlay: true),

        // Basit toplama. Say ve Eşleştir'de miktar bir rakamla eşleniyordu,
        // burada iki miktar birleşiyor; Noktaları Birleştir'in kurduğu sayı
        // doğrusu fikri de tam altında duruyor.
        //
        // En küçük bant Fidan: 2-4 yaş toplamıyor, o yaşın karşılığı Say ve
        // Eşleştir'in kendisi.
        new MiniGameDescriptor(
            Addition,
            InteractionKind.Tap,
            GameTier.Subscription,
            AgeBand.Fidan,
            RenderKind.Layout,
            IsEducational: true,
            SupportsPassAndPlay: true),
    ]);

    public static MiniGameDescriptor ById(string id) =>
        TryById(id) ?? throw new ArgumentException($"Bilinmeyen mini oyun: {id}", nameof(id));

    public static MiniGameDescriptor? TryById(string id) =>
        Games.FirstOrDefault(g => g.Id == id);

    /// <summary>
    /// Verilen bantta anlamlı olan oyunlar. Kilitli olanlar da dahildir —
    /// kilitli oyunu gizlemek yerine göstermek aboneliğin ne getirdiğini anlatır.
    /// </summary>
    public static IReadOnlyList<MiniGameDescriptor> ForBand(AgeBand band) =>
        Games.Where(g => g.MinBand <= band).ToList();

    public static IReadOnlyList<MiniGameDescriptor> Educational =>
        Games.Where(g => g.IsEducational).ToList();

    public static IReadOnlyList<MiniGameDescriptor> Free =>
        Games.Where(g => g.Tier == GameTier.Free).ToList();
}
