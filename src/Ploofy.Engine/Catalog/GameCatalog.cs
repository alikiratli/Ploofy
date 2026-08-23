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
