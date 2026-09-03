using Ploofy.App.Views;

namespace Ploofy.App;

public partial class AppShell : Shell
{
    public AppShell()
    {
        InitializeComponent();
        RegisterRoutes();
    }

    /// <summary>
    /// Kabuk ağacında görünmeyen, koda göre gidilen sayfalar.
    /// </summary>
    /// <remarks>
    /// Yeni mini oyun eklenirken buraya bir satır giriyor ve karşılığı
    /// <c>GamePresentation.Route</c> içinde tanımlanıyor.
    /// </remarks>
    private static void RegisterRoutes()
    {
        Routing.RegisterRoute("profileeditor", typeof(ProfileEditorPage));
        Routing.RegisterRoute("playsetup", typeof(PlaySetupPage));
        Routing.RegisterRoute("result", typeof(RoundResultPage));
        Routing.RegisterRoute("settings", typeof(SettingsPage));
        Routing.RegisterRoute("paywall", typeof(PaywallPage));
        Routing.RegisterRoute("subscription", typeof(SubscriptionPage));
        Routing.RegisterRoute("report", typeof(ReportPage));
        Routing.RegisterRoute("collection", typeof(CollectionPage));
        Routing.RegisterRoute("dottodot", typeof(DotToDotPage));
        Routing.RegisterRoute("categorysort", typeof(CategorySortPage));

        // Mini oyunlar
        Routing.RegisterRoute("memorymatch", typeof(MemoryMatchPage));
        Routing.RegisterRoute("bubblepop", typeof(BubblePopPage));
        Routing.RegisterRoute("shapesort", typeof(ShapeSortPage));

        Routing.RegisterRoute("countmatch", typeof(CountMatchPage));
        Routing.RegisterRoute("simon", typeof(SimonPage));
        Routing.RegisterRoute("basketcatch", typeof(BasketCatchPage));
        Routing.RegisterRoute("mazetrace", typeof(MazeTracePage));
        Routing.RegisterRoute("jigsaw", typeof(JigsawPage));
        Routing.RegisterRoute("lettertrace", typeof(LetterTracePage));
        Routing.RegisterRoute("pattern", typeof(PatternPage));
        Routing.RegisterRoute("lineup", typeof(LineUpPage));

        // Harf Avı ve Sayı Avı aynı mekaniği paylaşıyor; hangisi
        // olduğu oturumdaki oyun kimliğinden çözülüyor.
        Routing.RegisterRoute("hunt", typeof(HuntPage));
    }
}
